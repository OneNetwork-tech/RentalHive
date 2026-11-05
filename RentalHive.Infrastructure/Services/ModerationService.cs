using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Infrastructure.Services;

public sealed partial class ModerationService(
    RentalHiveDbContext db,
    IClock clock,
    IPersonalNumberProtector protector,
    BookingNotifier notifier,
    ILogger<ModerationService> logger) : IModerationService
{
    private const int MinReasonLength = 5;

    public async Task ReportListingAsync(int reporterId, int itemId, ReportListingRequest request, CancellationToken ct = default)
    {
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length < MinReasonLength) throw new DomainException("Tell the moderators what is wrong with the listing.");
        if (reason.Length > ListingReport.MaxReasonLength) throw new DomainException($"Use at most {ListingReport.MaxReasonLength} characters.");

        var item = await db.RentalItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Listing not found.", DomainErrorKind.NotFound);
        if (item.OwnerId == reporterId) throw new DomainException("You can't report your own listing.", DomainErrorKind.Forbidden);

        var open = await db.ListingReports.FirstOrDefaultAsync(r => r.RentalItemId == itemId && r.ReporterId == reporterId && r.ResolvedAt == null, ct)
            .ConfigureAwait(false);
        if (open is not null)
        {
            open.Reason = reason; // reporting again updates the existing report
        }
        else
        {
            db.ListingReports.Add(new ListingReport { RentalItemId = itemId, ReporterId = reporterId, Reason = reason, CreatedAt = clock.UtcNow });
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent duplicate; the report is already there.
        }

        LogReported(logger, itemId);
    }

    public async Task<IReadOnlyList<OpenReportDto>> GetOpenReportsAsync(int adminId, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var rows = await db.ListingReports.AsNoTracking()
            .Where(r => r.ResolvedAt == null)
            .OrderBy(r => r.CreatedAt)
            .Take(200)
            .Select(r => new
            {
                r.Id, r.RentalItemId, ItemName = r.RentalItem!.Name, ItemActive = r.RentalItem.IsActive, r.RentalItem.OwnerId,
                OwnerFirst = r.RentalItem.Owner!.FirstName, OwnerLast = r.RentalItem.Owner.LastName, OwnerEmail = r.RentalItem.Owner.Email,
                r.Reason, ReporterFirst = r.Reporter!.FirstName, ReporterLast = r.Reporter.LastName, r.CreatedAt,
                Count = db.ListingReports.Count(x => x.RentalItemId == r.RentalItemId && x.ResolvedAt == null),
            })
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => new OpenReportDto(r.Id, r.RentalItemId, r.ItemName, r.ItemActive, r.OwnerId, $"{r.OwnerFirst} {r.OwnerLast}", r.OwnerEmail,
            r.Reason, $"{r.ReporterFirst} {r.ReporterLast}", r.CreatedAt, r.Count)).ToList();
    }

    public async Task DismissReportAsync(int adminId, int reportId, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var now = clock.UtcNow;
        var n = await db.ListingReports.Where(r => r.Id == reportId && r.ResolvedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedAt, now).SetProperty(r => r.Resolution, "Dismissed"), ct).ConfigureAwait(false);
        if (n == 0) throw new DomainException("Report not found.", DomainErrorKind.NotFound);
    }

    public async Task RemoveListingAsync(int adminId, int itemId, string reason, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var why = RequireReason(reason);
        var now = clock.UtcNow;
        var item = await db.RentalItems.Include(i => i.Owner).FirstOrDefaultAsync(i => i.Id == itemId, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Listing not found.", DomainErrorKind.NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        item.IsActive = false;
        item.RemovedAt ??= now;
        item.RemovalReason = why;
        item.UpdatedAt = now;
        var cancelled = await EndPendingRequestsAsync(db.Bookings.Where(b => b.RentalItemId == itemId), now, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await ResolveReportsAsync(db.ListingReports.Where(r => r.RentalItemId == itemId), "Listing removed", now, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        LogRemoved(logger, itemId, adminId);
        notifier.Notice(item.Owner!, $"Your listing was removed: {item.Name}",
            $"A moderator removed your listing \"{item.Name}\" because it breaks the RentalHive rules:\n{why}\n\n" +
            "Pending requests for it were cancelled. Confirmed bookings stay in place: agree directly with the renter whether they go ahead.");
        await notifier.NoLongerAvailableAsync(cancelled, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MemberDto>> FindMembersAsync(int adminId, string query, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var q = (query ?? "").Trim();
        var users = db.Users.AsNoTracking();
        if (q.Length == 0)
        {
            users = users.Where(u => u.SuspendedAt != null || u.IsAdmin);
        }
        else if (!q.Contains('@', StringComparison.Ordinal) && (PersonalNumber.TryParse(q, clock.Today, out var pnr) | PhoneNumber.TryNormalize(q, out var phone)))
        {
            // A number can be both a valid personnummer and a phone number, so match either.
            var hash = pnr is null ? "" : protector.Hash(pnr.Value);
            var normalizedPhone = phone ?? "";
            users = users.Where(u => (hash != "" && u.PersonalNumberHash == hash) || (normalizedPhone != "" && u.PhoneNumber == normalizedPhone));
        }
        else
        {
            var pattern = "%" + q.ToLowerInvariant().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
            users = users.Where(u => EF.Functions.ILike(u.Email, pattern, "\\") || EF.Functions.ILike(u.FirstName + " " + u.LastName, pattern, "\\"));
        }

        return await users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).Take(50)
            .Select(u => new MemberDto(u.Id, u.FirstName + " " + u.LastName, u.Email, u.PhoneNumber, u.MemberSince, u.IsAdmin, u.SuspendedAt, u.SuspensionReason,
                u.OwnedItems.Count(i => i.IsActive)))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task SuspendAsync(int adminId, int userId, string reason, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var why = RequireReason(reason);
        if (userId == adminId) throw new DomainException("You can't suspend yourself.", DomainErrorKind.Conflict);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Member not found.", DomainErrorKind.NotFound);
        if (user.IsAdmin) throw new DomainException("Remove the member's moderator rights before suspending them.", DomainErrorKind.Conflict);

        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        user.SuspendedAt ??= now;
        user.SuspensionReason = why;
        user.SecurityStamp = Guid.NewGuid(); // ends web sessions and API tokens

        var listings = await db.RentalItems.Where(i => i.OwnerId == userId && i.IsActive).ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in listings)
        {
            item.IsActive = false;
            item.UpdatedAt = now;
        }

        var cancelled = await EndPendingRequestsAsync(db.Bookings.Where(b => b.RentalItem!.OwnerId == userId), now, ct).ConfigureAwait(false);
        // Their own open requests to other members are withdrawn.
        await EndPendingRequestsAsync(db.Bookings.Where(b => b.RenterId == userId), now, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await ResolveReportsAsync(db.ListingReports.Where(r => r.RentalItem!.OwnerId == userId), "Owner suspended", now, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        LogSuspended(logger, userId, adminId);
        notifier.Notice(user, "Your RentalHive account has been suspended",
            $"A moderator suspended your account:\n{why}\n\nYou can no longer sign in, and your listings are hidden. " +
            "Confirmed bookings stay in place; contact the other party directly. Reply to this email if you think this is a mistake.");
        await notifier.NoLongerAvailableAsync(cancelled, ct).ConfigureAwait(false);
    }

    public async Task UnsuspendAsync(int adminId, int userId, CancellationToken ct = default)
    {
        await RequireAdminAsync(adminId, ct).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Member not found.", DomainErrorKind.NotFound);
        if (!user.IsSuspended) return;
        user.SuspendedAt = null;
        user.SuspensionReason = null;
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        LogUnsuspended(logger, userId, adminId);
        notifier.Notice(user, "Your RentalHive account is active again",
            "A moderator lifted the suspension of your account. You can sign in again; reactivate your listings under My listings.");
    }

    public async Task<bool> SetAdminAsync(string email, bool isAdmin, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == e, ct).ConfigureAwait(false);
        if (user is null) return false;
        user.IsAdmin = isAdmin;
        user.SecurityStamp = Guid.NewGuid(); // the role claim is refreshed at the next sign-in
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    // --- helpers ---------------------------------------------------------------------

    private async Task RequireAdminAsync(int adminId, CancellationToken ct)
    {
        // Checked against the database on every call, not only the role claim.
        if (!await db.Users.AnyAsync(u => u.Id == adminId && u.IsAdmin && u.SuspendedAt == null, ct).ConfigureAwait(false))
            throw new DomainException("Only moderators can do this.", DomainErrorKind.Forbidden);
    }

    private static string RequireReason(string reason)
    {
        var why = (reason ?? "").Trim();
        if (why.Length < MinReasonLength) throw new DomainException("Give a reason; it is sent to the member.");
        return why.Length > 500 ? why[..500] : why;
    }

    /// <summary>Ends pending requests in <paramref name="bookings"/> and returns their ids.</summary>
    private async Task<List<int>> EndPendingRequestsAsync(IQueryable<Booking> bookings, DateTime now, CancellationToken ct)
    {
        var pending = await bookings.Where(b => b.Status == BookingStatus.Pending).ToListAsync(ct).ConfigureAwait(false);
        foreach (var b in pending) b.WithdrawBySystem(now);
        return pending.Select(b => b.Id).ToList();
    }

    private static Task<int> ResolveReportsAsync(IQueryable<ListingReport> reports, string resolution, DateTime now, CancellationToken ct) =>
        reports.Where(r => r.ResolvedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedAt, now).SetProperty(r => r.Resolution, resolution), ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listing {ItemId} reported")]
    private static partial void LogReported(ILogger logger, int itemId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Listing {ItemId} removed by moderator {AdminId}")]
    private static partial void LogRemoved(ILogger logger, int itemId, int adminId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Member {UserId} suspended by moderator {AdminId}")]
    private static partial void LogSuspended(ILogger logger, int userId, int adminId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Member {UserId} unsuspended by moderator {AdminId}")]
    private static partial void LogUnsuspended(ILogger logger, int userId, int adminId);
}

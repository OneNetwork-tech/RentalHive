using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Infrastructure.Services;

public sealed partial class BookingService(RentalHiveDbContext db, IClock clock, BookingNotifier notifier, ILogger<BookingService> logger) : IBookingService
{
    /// <summary>The change is already saved; a notification problem must not turn it into an error.</summary>
    private async Task NotifyAsync(Func<Task> send)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not queue a booking notification")]
    private static partial void LogNotifyFailed(ILogger logger, Exception ex);

    public async Task<Quote> QuoteAsync(int itemId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var item = await db.RentalItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Listing not found.", DomainErrorKind.NotFound);
        var days = Booking.ValidatePeriod(start, end, clock.Today);
        var available = !await OverlapsConfirmedAsync(itemId, start, end, null, ct).ConfigureAwait(false);
        return new Quote(days, item.DailyRate, Booking.Price(item.DailyRate, days), available);
    }

    public async Task<BookingDto> RequestAsync(int renterId, BookingRequest r, CancellationToken ct = default)
    {
        var item = await db.RentalItems.FirstOrDefaultAsync(i => i.Id == r.RentalItemId, ct).ConfigureAwait(false)
                   ?? throw new DomainException("Listing not found.", DomainErrorKind.NotFound);
        var booking = Booking.Request(item, renterId, r.StartDate, r.EndDate, r.Message, clock.Today, clock.UtcNow);
        if (await OverlapsConfirmedAsync(item.Id, r.StartDate, r.EndDate, null, ct).ConfigureAwait(false))
            throw new DomainException("The item is already booked for some of these dates.", DomainErrorKind.Conflict);
        if (await db.Bookings.AnyAsync(b => b.RentalItemId == item.Id && b.RenterId == renterId && b.Status == BookingStatus.Pending
                                            && b.StartDate < r.EndDate && r.StartDate < b.EndDate, ct).ConfigureAwait(false))
            throw new DomainException("You already have a pending request for these dates.", DomainErrorKind.Conflict);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await NotifyAsync(() => notifier.RequestedAsync(booking.Id, ct)).ConfigureAwait(false);
        return await LoadAsync(booking.Id, renterId, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BookingDto>> GetAsRenterAsync(int renterId, CancellationToken ct = default) =>
        ListAsync(db.Bookings.Where(b => b.RenterId == renterId), renterId, asOwner: false, ct);

    public Task<IReadOnlyList<BookingDto>> GetAsOwnerAsync(int ownerId, CancellationToken ct = default) =>
        ListAsync(db.Bookings.Where(b => b.RentalItem!.OwnerId == ownerId), ownerId, asOwner: true, ct);

    public async Task<BookingDto> ConfirmAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var (booking, ownerId) = await TrackedAsync(bookingId, userId, ct).ConfigureAwait(false);
        booking.Confirm(userId, ownerId, clock.UtcNow);
        if (booking.StartDate < clock.Today) throw new DomainException("The rental period has already started; decline the request instead.", DomainErrorKind.Conflict);

        // Overlapping pending requests can no longer be fulfilled.
        var others = await db.Bookings.Where(b => b.RentalItemId == booking.RentalItemId && b.Id != booking.Id && b.Status == BookingStatus.Pending
                                                 && b.StartDate < booking.EndDate && booking.StartDate < b.EndDate)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var o in others) o.DeclineAsUnavailable(clock.UtcNow);
        await SaveAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        var declined = others.Select(o => o.Id).ToList();
        await NotifyAsync(() => notifier.ConfirmedAsync(bookingId, declined, ct)).ConfigureAwait(false);
        return await LoadAsync(bookingId, userId, ct).ConfigureAwait(false);
    }

    public async Task<BookingDto> DeclineAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var (booking, ownerId) = await TrackedAsync(bookingId, userId, ct).ConfigureAwait(false);
        booking.Decline(userId, ownerId, clock.UtcNow);
        await SaveAsync(ct).ConfigureAwait(false);
        await NotifyAsync(() => notifier.DeclinedAsync(bookingId, ct)).ConfigureAwait(false);
        return await LoadAsync(bookingId, userId, ct).ConfigureAwait(false);
    }

    public async Task<BookingDto> CancelAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var (booking, ownerId) = await TrackedAsync(bookingId, userId, ct).ConfigureAwait(false);
        booking.Cancel(userId, ownerId, clock.Today, clock.UtcNow);
        await SaveAsync(ct).ConfigureAwait(false);
        await NotifyAsync(() => notifier.CancelledAsync(bookingId, userId, ct)).ConfigureAwait(false);
        return await LoadAsync(bookingId, userId, ct).ConfigureAwait(false);
    }

    public async Task<BookingDto> CompleteAsync(int userId, int bookingId, CancellationToken ct = default)
    {
        var (booking, ownerId) = await TrackedAsync(bookingId, userId, ct).ConfigureAwait(false);
        booking.Complete(userId, ownerId, clock.Today, clock.UtcNow);
        await SaveAsync(ct).ConfigureAwait(false);
        await NotifyAsync(() => notifier.CompletedAsync(bookingId, ct)).ConfigureAwait(false);
        return await LoadAsync(bookingId, userId, ct).ConfigureAwait(false);
    }

    public async Task<ReviewDto> ReviewAsync(int userId, int bookingId, ReviewRequest r, CancellationToken ct = default)
    {
        if (r.Rating is < 1 or > 5) throw new DomainException("Choose 1–5 stars.");
        var booking = await db.Bookings.Include(b => b.Review).FirstOrDefaultAsync(b => b.Id == bookingId, ct).ConfigureAwait(false);
        if (booking is null || booking.RenterId != userId) throw new DomainException("Booking not found.", DomainErrorKind.NotFound);
        if (booking.Status != BookingStatus.Completed) throw new DomainException("You can review a rental once it has been completed.", DomainErrorKind.Conflict);
        if (booking.Review is not null) throw new DomainException("You have already reviewed this rental.", DomainErrorKind.Conflict);
        var review = new Review
        {
            BookingId = booking.Id,
            RentalItemId = booking.RentalItemId,
            AuthorId = userId,
            Rating = r.Rating,
            Comment = (r.Comment ?? "").Trim(),
            CreatedAt = clock.UtcNow,
        };
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new DomainException("You have already reviewed this rental.", DomainErrorKind.Conflict);
        }
        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        return new ReviewDto(review.Id, review.Rating, review.Comment, author.DisplayName, review.CreatedAt);
    }

    // --- helpers ---------------------------------------------------------------------

    private Task<bool> OverlapsConfirmedAsync(int itemId, DateOnly start, DateOnly end, int? exceptId, CancellationToken ct) =>
        db.Bookings.AnyAsync(b => b.RentalItemId == itemId && b.Status == BookingStatus.Confirmed && b.Id != (exceptId ?? 0)
                                  && b.StartDate < end && start < b.EndDate, ct);

    /// <summary>Load a booking the user is a party to (as renter or owner), tracked for update.</summary>
    private async Task<(Booking Booking, int OwnerId)> TrackedAsync(int bookingId, int userId, CancellationToken ct)
    {
        var booking = await db.Bookings.Include(b => b.RentalItem).FirstOrDefaultAsync(b => b.Id == bookingId, ct).ConfigureAwait(false);
        if (booking is null || (booking.RenterId != userId && booking.RentalItem!.OwnerId != userId))
            throw new DomainException("Booking not found.", DomainErrorKind.NotFound);
        return (booking, booking.RentalItem!.OwnerId);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsExclusionViolation())
        {
            throw new DomainException("Another booking was confirmed for overlapping dates.", DomainErrorKind.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("The booking was changed by someone else. Reload and try again.", DomainErrorKind.Conflict);
        }
    }

    private async Task<BookingDto> LoadAsync(int bookingId, int viewerId, CancellationToken ct)
    {
        var list = await ListAsync(db.Bookings.Where(b => b.Id == bookingId), viewerId, null, ct).ConfigureAwait(false);
        return list.Single();
    }

    private async Task<IReadOnlyList<BookingDto>> ListAsync(IQueryable<Booking> query, int viewerId, bool? asOwner, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .Include(b => b.RentalItem).ThenInclude(i => i!.Images)
            .Include(b => b.RentalItem).ThenInclude(i => i!.Owner)
            .Include(b => b.Renter)
            .Include(b => b.Review)
            .AsSplitQuery()
            .OrderByDescending(b => b.StartDate).ThenByDescending(b => b.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(b =>
        {
            var item = b.RentalItem!;
            var viewerIsOwner = asOwner ?? item.OwnerId == viewerId;
            var counterpart = viewerIsOwner ? b.Renter! : item.Owner!;
            // Contact details are shared only once a booking is confirmed (or completed).
            var share = b.Status is BookingStatus.Confirmed or BookingStatus.Completed;
            return new BookingDto(
                b.Id, item.Id, item.Name, item.Category, item.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.FileName, item.City,
                b.StartDate, b.EndDate, b.Days, b.DailyRate, b.TotalCost, b.Status, b.Message,
                counterpart.DisplayName, share ? counterpart.Email : null, share ? counterpart.PhoneNumber : null,
                b.CreatedAt,
                CanReview: !viewerIsOwner && b.Status == BookingStatus.Completed && b.Review is null,
                ReviewRating: b.Review?.Rating);
        }).ToList();
    }
}

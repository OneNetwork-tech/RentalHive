using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentalHive.Application.Contracts;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Infrastructure.Services;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Public address of the web app, used for links in emails (e.g. https://rentalhive.example.se).</summary>
    public string? PublicUrl { get; set; }
}

/// <summary>
/// Tells the other party when a booking changes. Emails contain the listing name, dates and
/// display names only; contact details are included once a booking is confirmed, as in the app.
/// </summary>
public sealed class BookingNotifier(RentalHiveDbContext db, IEmailSender email, IOptions<AppOptions> app)
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    private sealed record Parties(Booking Booking, RentalItem Item, User Owner, User Renter);

    private async Task<Parties?> LoadAsync(int bookingId, CancellationToken ct)
    {
        var b = await db.Bookings.AsNoTracking()
            .Include(x => x.RentalItem).ThenInclude(i => i!.Owner)
            .Include(x => x.Renter)
            .FirstOrDefaultAsync(x => x.Id == bookingId, ct).ConfigureAwait(false);
        return b is null ? null : new Parties(b, b.RentalItem!, b.RentalItem!.Owner!, b.Renter!);
    }

    private string Link(string path)
    {
        var url = app.Value.PublicUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(url) ? string.Empty : $"\n{url}{path}\n";
    }

    private static string Period(Booking b) =>
        string.Create(Sv, $"{b.StartDate:yyyy-MM-dd} (pick-up) to {b.EndDate:yyyy-MM-dd} (return), {b.Days} day{(b.Days == 1 ? "" : "s")}, {Money(b.TotalCost)}");

    private static string Money(decimal amount) =>
        amount == decimal.Truncate(amount) ? string.Create(Sv, $"{amount:#,0} kr") : string.Create(Sv, $"{amount:#,0.00} kr");

    private void Send(User to, string subject, string body) =>
        email.Enqueue(new EmailMessage(to.Email, subject, $"Hi {to.FirstName},\n\n{body}\n\n– RentalHive"));

    public async Task RequestedAsync(int bookingId, CancellationToken ct = default)
    {
        if (await LoadAsync(bookingId, ct).ConfigureAwait(false) is not { } p) return;
        var message = string.IsNullOrWhiteSpace(p.Booking.Message) ? "" : $"\n\nTheir message:\n{p.Booking.Message}";
        Send(p.Owner, $"New booking request: {p.Item.Name}",
            $"{p.Renter.DisplayName} wants to rent \"{p.Item.Name}\":\n{Period(p.Booking)}{message}\n\nConfirm or decline the request:{Link("/my/requests")}");
    }

    public async Task ConfirmedAsync(int bookingId, IEnumerable<int> declinedAsUnavailable, CancellationToken ct = default)
    {
        if (await LoadAsync(bookingId, ct).ConfigureAwait(false) is not { } p) return;
        Send(p.Renter, $"Booking confirmed: {p.Item.Name}",
            $"{p.Owner.DisplayName} confirmed your booking of \"{p.Item.Name}\":\n{Period(p.Booking)}\n\n" +
            $"Contact the owner to agree on pick-up and payment:\n{p.Owner.FirstName} {p.Owner.LastName}, {p.Owner.Email}, {p.Owner.PhoneNumber}\n\nYour bookings:{Link("/my/bookings")}");
        foreach (var id in declinedAsUnavailable)
        {
            if (await LoadAsync(id, ct).ConfigureAwait(false) is not { } other) continue;
            Send(other.Renter, $"Not available: {other.Item.Name}",
                $"\"{other.Item.Name}\" was booked by someone else for some of the dates you asked for ({Period(other.Booking)}), so your request was declined.\n\nFind another item:{Link("/search")}");
        }
    }

    /// <summary>Pending requests that ended because a moderator took the listing or its owner down.</summary>
    public async Task NoLongerAvailableAsync(IEnumerable<int> bookingIds, CancellationToken ct = default)
    {
        foreach (var id in bookingIds)
        {
            if (await LoadAsync(id, ct).ConfigureAwait(false) is not { } p) continue;
            Send(p.Renter, $"Not available: {p.Item.Name}",
                $"\"{p.Item.Name}\" is no longer available, so your request ({Period(p.Booking)}) was cancelled.\n\nFind another item:{Link("/search")}");
        }
    }

    /// <summary>Plain notice to a member from the moderators.</summary>
    public void Notice(User to, string subject, string body) => Send(to, subject, body);

    public async Task DeclinedAsync(int bookingId, CancellationToken ct = default)
    {
        if (await LoadAsync(bookingId, ct).ConfigureAwait(false) is not { } p) return;
        Send(p.Renter, $"Request declined: {p.Item.Name}",
            $"{p.Owner.DisplayName} declined your request for \"{p.Item.Name}\" ({Period(p.Booking)}).\n\nFind another item:{Link("/search")}");
    }

    public async Task CancelledAsync(int bookingId, int cancelledBy, CancellationToken ct = default)
    {
        if (await LoadAsync(bookingId, ct).ConfigureAwait(false) is not { } p) return;
        var (to, by, path) = cancelledBy == p.Renter.Id ? (p.Owner, p.Renter, "/my/requests") : (p.Renter, p.Owner, "/my/bookings");
        Send(to, $"Booking cancelled: {p.Item.Name}",
            $"{by.DisplayName} cancelled the booking of \"{p.Item.Name}\" ({Period(p.Booking)}).{Link(path)}");
    }

    public async Task CompletedAsync(int bookingId, CancellationToken ct = default)
    {
        if (await LoadAsync(bookingId, ct).ConfigureAwait(false) is not { } p) return;
        Send(p.Renter, $"Thanks for renting {p.Item.Name}",
            $"{p.Owner.DisplayName} marked \"{p.Item.Name}\" as returned. You can now leave a review:{Link("/my/bookings")}");
    }
}

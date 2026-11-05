using RentalHive.Domain.Common;

namespace RentalHive.Domain.Entities;

public enum BookingStatus
{
    /// <summary>Requested by the renter, awaiting the owner's answer.</summary>
    Pending = 0,
    Confirmed = 1,
    Declined = 2,
    Cancelled = 3,
    Completed = 4,
}

/// <summary>
/// A rental of one item for a period. StartDate is the pick-up day and EndDate
/// the return day; the item is occupied for [StartDate, EndDate) and the
/// price is charged per day in that range.
/// </summary>
public class Booking
{
    public const int MaxDays = 90;
    public const int MaxDaysAhead = 365;

    public int Id { get; set; }
    public int RentalItemId { get; set; }
    public RentalItem? RentalItem { get; set; }
    public int RenterId { get; set; }
    public User? Renter { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }

    /// <summary>Daily rate at the time of booking (later price changes don't affect it).</summary>
    public decimal DailyRate { get; set; }

    public decimal TotalCost { get; set; }
    public BookingStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint Version { get; set; }

    public Review? Review { get; set; }

    /// <summary>Validate a requested period and return the number of rental days.</summary>
    public static int ValidatePeriod(DateOnly start, DateOnly end, DateOnly today)
    {
        if (start < today) throw new DomainException("The rental cannot start in the past.");
        if (end <= start) throw new DomainException("The return date must be after the pick-up date.");
        if (start > today.AddDays(MaxDaysAhead)) throw new DomainException($"Bookings can be made at most {MaxDaysAhead} days ahead.");
        var days = end.DayNumber - start.DayNumber;
        if (days > MaxDays) throw new DomainException($"A rental can last at most {MaxDays} days.");
        return days;
    }

    public static decimal Price(decimal dailyRate, int days) => decimal.Round(dailyRate * days, 2, MidpointRounding.AwayFromZero);

    public static Booking Request(RentalItem item, int renterId, DateOnly start, DateOnly end, string? message, DateOnly today, DateTime now)
    {
        if (!item.IsActive) throw new DomainException("This item is not available for rent.");
        if (item.OwnerId == renterId) throw new DomainException("You cannot book your own item.", DomainErrorKind.Forbidden);
        var days = ValidatePeriod(start, end, today);
        return new Booking
        {
            RentalItemId = item.Id,
            RenterId = renterId,
            StartDate = start,
            EndDate = end,
            Days = days,
            DailyRate = item.DailyRate,
            TotalCost = Price(item.DailyRate, days),
            Status = BookingStatus.Pending,
            Message = (message ?? string.Empty).Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool Overlaps(DateOnly start, DateOnly end) => StartDate < end && start < EndDate;

    public void Confirm(int actingUserId, int ownerId, DateTime now)
    {
        RequireOwner(actingUserId, ownerId);
        RequireStatus(BookingStatus.Pending, "Only pending requests can be confirmed.");
        Status = BookingStatus.Confirmed;
        UpdatedAt = now;
    }

    public void Decline(int actingUserId, int ownerId, DateTime now)
    {
        RequireOwner(actingUserId, ownerId);
        RequireStatus(BookingStatus.Pending, "Only pending requests can be declined.");
        Status = BookingStatus.Declined;
        UpdatedAt = now;
    }

    /// <summary>Decline because another request for overlapping dates was confirmed.</summary>
    public void DeclineAsUnavailable(DateTime now)
    {
        if (Status != BookingStatus.Pending) return;
        Status = BookingStatus.Declined;
        UpdatedAt = now;
    }

    /// <summary>The renter can cancel before the rental starts; the owner can cancel a pending or confirmed booking before it starts.</summary>
    /// <summary>Cancels a pending request without a party acting (the renter's account was suspended).</summary>
    public void WithdrawBySystem(DateTime now)
    {
        if (Status != BookingStatus.Pending) return;
        Status = BookingStatus.Cancelled;
        UpdatedAt = now;
    }

    public void Cancel(int actingUserId, int ownerId, DateOnly today, DateTime now)
    {
        if (actingUserId != RenterId && actingUserId != ownerId) throw new DomainException("You cannot change this booking.", DomainErrorKind.Forbidden);
        if (Status is not (BookingStatus.Pending or BookingStatus.Confirmed)) throw new DomainException("Only pending or confirmed bookings can be cancelled.", DomainErrorKind.Conflict);
        if (today >= StartDate) throw new DomainException("A booking cannot be cancelled once the rental has started.", DomainErrorKind.Conflict);
        Status = BookingStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>The owner marks the rental as completed once the item has been returned.</summary>
    public void Complete(int actingUserId, int ownerId, DateOnly today, DateTime now)
    {
        RequireOwner(actingUserId, ownerId);
        RequireStatus(BookingStatus.Confirmed, "Only confirmed bookings can be completed.");
        if (today < EndDate) throw new DomainException("The booking can be completed on or after the return date.", DomainErrorKind.Conflict);
        Status = BookingStatus.Completed;
        UpdatedAt = now;
    }

    private static void RequireOwner(int actingUserId, int ownerId)
    {
        if (actingUserId != ownerId) throw new DomainException("Only the owner can do this.", DomainErrorKind.Forbidden);
    }

    private void RequireStatus(BookingStatus expected, string message)
    {
        if (Status != expected) throw new DomainException(message, DomainErrorKind.Conflict);
    }
}

public class Review
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int RentalItemId { get; set; }
    public RentalItem? RentalItem { get; set; }
    public int AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>1–5 stars.</summary>
    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

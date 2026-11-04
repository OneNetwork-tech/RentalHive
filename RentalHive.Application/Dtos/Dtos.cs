using System.ComponentModel.DataAnnotations;
using RentalHive.Domain.Entities;

namespace RentalHive.Application.Dtos;

// --- Accounts -------------------------------------------------------------------

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Personnummer is required.")]
    public string PersonalNumber { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 10, ErrorMessage = "The password must be at least 10 characters.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    /// <summary>Email, personnummer or phone number.</summary>
    [Required(ErrorMessage = "Enter your email, personnummer or phone number.")]
    public string LoginIdentifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 10, ErrorMessage = "The password must be at least 10 characters.")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;
}

public sealed record UserSummary(int Id, string Email, string FirstName, string LastName, string DisplayName, Guid SecurityStamp);

public sealed record ProfileDto(
    int Id, string Email, string PhoneNumber, string FirstName, string LastName, string Address, string City,
    string PersonalNumberMasked, DateTime MemberSince);

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
}

public sealed record LoginResult(LoginOutcome Outcome, UserSummary? User, DateTime? LockedUntilUtc = null);

// --- Listings -------------------------------------------------------------------

/// <summary>Create/update a listing. Category-specific fields are validated per category.</summary>
public sealed class ListingInput
{
    [Required]
    public ItemCategory Category { get; set; }

    [Required, StringLength(120, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 100000, ErrorMessage = "The daily rate must be between 1 and 100 000 kr.")]
    public decimal DailyRate { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    // Car, Van, Minibus, LightTruck
    [StringLength(60)] public string? Make { get; set; }
    [StringLength(60)] public string? Model { get; set; }
    public int? Year { get; set; }
    public int? NumberOfSeats { get; set; }
    public Transmission? Transmission { get; set; }
    public FuelType? FuelType { get; set; }

    // Van
    public double? LoadCapacityInCubicMeters { get; set; }
    public bool? HasSlidingDoor { get; set; }

    // Minibus
    public bool? IsWheelchairAccessible { get; set; }

    // LightTruck
    public double? PayloadCapacityInKg { get; set; }
    public bool? HasTailLift { get; set; }

    // Trailer, CarTransporter, BoatTrailer
    public double? LoadCapacityInKg { get; set; }
    public double? DeckWidthInMeters { get; set; }
    public double? DeckLengthInMeters { get; set; }
    public bool? HasBrakes { get; set; }
    public int? NumberOfAxles { get; set; }
    public bool? HasWinch { get; set; }
    public int? MaxBoatLengthInFeet { get; set; }
    public bool? IsSubmersible { get; set; }

    // Boat
    [StringLength(60)] public string? BoatType { get; set; }
    public int? LengthInFeet { get; set; }
    public int? Capacity { get; set; }
    public bool? RequiresLicense { get; set; }

    // Machine
    [StringLength(60)] public string? MachineType { get; set; }
    public int? Horsepower { get; set; }
    public double? WeightInKg { get; set; }
}

public sealed class ListingSearch
{
    public ItemCategory? Category { get; set; }
    public string? City { get; set; }
    public string? Query { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>Only items free for the whole [From, To) period.</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
    public ListingSort Sort { get; set; } = ListingSort.Newest;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public enum ListingSort
{
    Newest,
    PriceLow,
    PriceHigh,
    Rating,
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record ListingSummary(
    int Id, ItemCategory Category, string Name, string City, decimal DailyRate, string? ImageFileName,
    double? AverageRating, int ReviewCount, string Subtitle, bool IsActive);

public sealed record ListingDetails(
    int Id, ItemCategory Category, string Name, string Description, string City, decimal DailyRate, bool IsActive,
    int OwnerId, string OwnerName, DateTime OwnerMemberSince, IReadOnlyList<ImageDto> Images,
    IReadOnlyList<SpecDto> Specs, double? AverageRating, IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<DateRange> UnavailablePeriods, ListingInput Input);

public sealed record ImageDto(int Id, string FileName);

public sealed record SpecDto(string Label, string Value);

public sealed record DateRange(DateOnly Start, DateOnly End);

public sealed record ReviewDto(int Id, int Rating, string Comment, string AuthorName, DateTime CreatedAt);

// --- Bookings -------------------------------------------------------------------

public sealed class BookingRequest
{
    [Required]
    public int RentalItemId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }
}

public sealed record Quote(int Days, decimal DailyRate, decimal Total, bool Available);

public sealed record BookingDto(
    int Id, int RentalItemId, string ItemName, ItemCategory Category, string? ImageFileName, string City,
    DateOnly StartDate, DateOnly EndDate, int Days, decimal DailyRate, decimal TotalCost, BookingStatus Status,
    string Message, string CounterpartName, string? CounterpartEmail, string? CounterpartPhone,
    DateTime CreatedAt, bool CanReview, int? ReviewRating);

public sealed class ReviewRequest
{
    [Range(1, 5, ErrorMessage = "Choose 1–5 stars.")]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string Comment { get; set; } = string.Empty;
}

using RentalHive.Application.Dtos;

namespace RentalHive.Application.Contracts;

public interface IAccountService
{
    Task<UserSummary> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserSummary?> FindAsync(int userId, CancellationToken ct = default);
    Task<ProfileDto> GetProfileAsync(int userId, CancellationToken ct = default);
    Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>Changes the password and rotates the security stamp (signs out everywhere).</summary>
    Task<UserSummary> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>True when the user exists and the stamp in their session is current.</summary>
    Task<bool> IsSessionValidAsync(int userId, Guid securityStamp, CancellationToken ct = default);
}

public interface IListingService
{
    Task<PagedResult<ListingSummary>> SearchAsync(ListingSearch search, CancellationToken ct = default);
    Task<ListingDetails?> GetAsync(int id, int? viewerId, CancellationToken ct = default);
    Task<IReadOnlyList<ListingSummary>> GetOwnedAsync(int ownerId, CancellationToken ct = default);
    Task<int> CreateAsync(int ownerId, ListingInput input, CancellationToken ct = default);
    Task UpdateAsync(int ownerId, int id, ListingInput input, CancellationToken ct = default);
    Task SetActiveAsync(int ownerId, int id, bool active, CancellationToken ct = default);

    /// <summary>Deletes the listing, or deactivates it when it has booking history.</summary>
    Task<bool> DeleteAsync(int ownerId, int id, CancellationToken ct = default);

    Task<ImageDto> AddImageAsync(int ownerId, int id, Stream content, long length, CancellationToken ct = default);
    Task DeleteImageAsync(int ownerId, int id, int imageId, CancellationToken ct = default);
}

public interface IBookingService
{
    Task<Quote> QuoteAsync(int itemId, DateOnly start, DateOnly end, CancellationToken ct = default);
    Task<BookingDto> RequestAsync(int renterId, BookingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> GetAsRenterAsync(int renterId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> GetAsOwnerAsync(int ownerId, CancellationToken ct = default);
    Task<BookingDto> ConfirmAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<BookingDto> DeclineAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<BookingDto> CancelAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<BookingDto> CompleteAsync(int userId, int bookingId, CancellationToken ct = default);
    Task<ReviewDto> ReviewAsync(int userId, int bookingId, ReviewRequest request, CancellationToken ct = default);
}

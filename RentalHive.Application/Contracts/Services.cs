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

public interface IModerationService
{
    /// <summary>A member flags a listing for moderators. One open report per member and listing.</summary>
    Task ReportListingAsync(int reporterId, int itemId, ReportListingRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<OpenReportDto>> GetOpenReportsAsync(int adminId, CancellationToken ct = default);
    Task DismissReportAsync(int adminId, int reportId, CancellationToken ct = default);

    /// <summary>Takes a listing down for good (the owner can't reactivate it) and resolves its reports.</summary>
    Task RemoveListingAsync(int adminId, int itemId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<MemberDto>> FindMembersAsync(int adminId, string query, CancellationToken ct = default);

    /// <summary>Blocks sign-in, ends sessions and tokens, and takes the member's listings down.</summary>
    Task SuspendAsync(int adminId, int userId, string reason, CancellationToken ct = default);
    Task UnsuspendAsync(int adminId, int userId, CancellationToken ct = default);

    /// <summary>Grants or revokes moderator rights by email (server command line only).</summary>
    Task<bool> SetAdminAsync(string email, bool isAdmin, CancellationToken ct = default);
}

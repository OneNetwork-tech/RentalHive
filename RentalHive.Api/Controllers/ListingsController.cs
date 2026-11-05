using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;

namespace RentalHive.Api.Controllers;

[ApiController]
[Route("api/listings")]
[Authorize]
public sealed class ListingsController(IListingService listings, IBookingService bookings, IModerationService moderation) : ControllerBase
{
    /// <summary>Flag a listing for the moderators (fake, misleading, dangerous…).</summary>
    [HttpPost("{id:int}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Report(int id, ReportListingRequest request, CancellationToken ct)
    {
        await moderation.ReportListingAsync(User.UserId(), id, request, ct);
        return NoContent();
    }

    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<ListingSummary>> Search([FromQuery] ListingSearch search, CancellationToken ct) => listings.SearchAsync(search, ct);

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ListingDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        int? viewer = User.Identity?.IsAuthenticated == true ? User.UserId() : null;
        var item = await listings.GetAsync(id, viewer, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}/quote")]
    [AllowAnonymous]
    public Task<Quote> Quote(int id, [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct) =>
        bookings.QuoteAsync(id, start, end, ct);

    [HttpGet("mine")]
    public Task<IReadOnlyList<ListingSummary>> Mine(CancellationToken ct) => listings.GetOwnedAsync(User.UserId(), ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ListingInput input, CancellationToken ct)
    {
        var id = await listings.CreateAsync(User.UserId(), input, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(int id, ListingInput input, CancellationToken ct)
    {
        await listings.UpdateAsync(User.UserId(), id, input, ct);
        return NoContent();
    }

    public sealed record ActiveRequest(bool Active);

    [HttpPut("{id:int}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(int id, ActiveRequest body, CancellationToken ct)
    {
        await listings.SetActiveAsync(User.UserId(), id, body.Active, ct);
        return NoContent();
    }

    /// <summary>Deletes the listing; listings with booking history are deactivated instead.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await listings.DeleteAsync(User.UserId(), id, ct);
        return Ok(new { deleted, deactivated = !deleted });
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<ImageDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var image = await listings.AddImageAsync(User.UserId(), id, stream, file.Length, ct);
        return StatusCode(StatusCodes.Status201Created, image);
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        await listings.DeleteImageAsync(User.UserId(), id, imageId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>Request a booking. The renter is always the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(BookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.RequestAsync(User.UserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, booking);
    }

    [HttpGet]
    public Task<IReadOnlyList<BookingDto>> Mine(CancellationToken ct) => bookings.GetAsRenterAsync(User.UserId(), ct);

    [HttpGet("incoming")]
    public Task<IReadOnlyList<BookingDto>> Incoming(CancellationToken ct) => bookings.GetAsOwnerAsync(User.UserId(), ct);

    [HttpPost("{id:int}/confirm")]
    public Task<BookingDto> Confirm(int id, CancellationToken ct) => bookings.ConfirmAsync(User.UserId(), id, ct);

    [HttpPost("{id:int}/decline")]
    public Task<BookingDto> Decline(int id, CancellationToken ct) => bookings.DeclineAsync(User.UserId(), id, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<BookingDto> Cancel(int id, CancellationToken ct) => bookings.CancelAsync(User.UserId(), id, ct);

    [HttpPost("{id:int}/complete")]
    public Task<BookingDto> Complete(int id, CancellationToken ct) => bookings.CompleteAsync(User.UserId(), id, ct);

    [HttpPost("{id:int}/review")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Review(int id, ReviewRequest request, CancellationToken ct)
    {
        var review = await bookings.ReviewAsync(User.UserId(), id, request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }
}

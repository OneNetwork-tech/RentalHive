using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Application.DTOs.Booking;
using RentalHive.Domain.Entities;

namespace RentalHive.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IRentalItemRepository _rentalItemRepository;

        public BookingsController(IBookingRepository bookingRepository, IRentalItemRepository rentalItemRepository)
        {
            _bookingRepository = bookingRepository;
            _rentalItemRepository = rentalItemRepository;
        }

        [HttpPost]
        [ProducesResponseType(typeof(Booking), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateBooking([FromBody] BookingCreateDto createDto)
        {
            if (!ModelState.IsValid || createDto.StartDate >= createDto.EndDate)
            {
                return BadRequest("Invalid booking request. Ensure start date is before end date.");
            }

            // In a real application, you would add logic here to check for booking conflicts
            // (i.e., ensure the item is not already booked for the selected dates).

            var rentalItem = await _rentalItemRepository.GetByIdAsync(createDto.RentalItemId);
            if (rentalItem == null)
            {
                return BadRequest("The specified rental item does not exist.");
            }

            // Calculate total cost
            var durationInDays = (createDto.EndDate - createDto.StartDate).Days;
            var totalCost = durationInDays * rentalItem.DailyRate;

            var booking = new Booking
            {
                RentalItemId = createDto.RentalItemId,
                RenterId = createDto.RenterId,
                StartDate = createDto.StartDate.ToUniversalTime(),
                EndDate = createDto.EndDate.ToUniversalTime(),
                TotalCost = totalCost,
                Status = "Confirmed" // Or "Pending" if you require owner approval
            };

            var createdBooking = await _bookingRepository.AddAsync(booking);

            return StatusCode(201, createdBooking);
        }
    }
}

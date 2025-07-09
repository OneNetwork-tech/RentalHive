using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.Booking
{
    /// <summary>
    /// DTO for creating a new booking.
    /// </summary>
    public class BookingCreateDto
    {
        [Required]
        public int RentalItemId { get; set; }

        [Required]
        public int RenterId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }
}

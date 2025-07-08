using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.RentalItem
{
    /// <summary>
    /// DTO for creating a new Car rental item.
    /// </summary>
    public class CreateCarDto
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }

        [Required]
        [Range(1, 100000)]
        public decimal DailyRate { get; set; }

        [Required]
        public int OwnerId { get; set; } // The ID of the user who owns this car

        [Required]
        public string Make { get; set; }

        [Required]
        public string Model { get; set; }

        [Required]
        [Range(1980, 2100)]
        public int Year { get; set; }

        [Required]
        [Range(1, 10)]
        public int NumberOfSeats { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
    }
}

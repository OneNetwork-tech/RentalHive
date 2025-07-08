using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.RentalItem
{
    public class RentalItemCreateDto
    {
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        [Range(1, 100000)]
        public decimal DailyRate { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Required]
        public string Category { get; set; } // "Car", "Boat", "Machine", etc.

        // Car-specific properties
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public int? NumberOfSeats { get; set; }
        public string? TransmissionType { get; set; }
        public string? FuelType { get; set; }

        // Boat-specific properties
        public string? BoatType { get; set; }
        public int? LengthInFeet { get; set; }
        public int? Capacity { get; set; }

        // Machine-specific properties
        public string? MachineType { get; set; }
        public int? Horsepower { get; set; }
        public double? WeightInKg { get; set; }
    }
}

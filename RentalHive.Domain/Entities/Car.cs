namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a car available for rent. Inherits from RentalItem.
    /// </summary>
    public class Car : RentalItem
    {
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public int NumberOfSeats { get; set; }
        public string TransmissionType { get; set; } // e.g., "Automatic", "Manual"
        public string FuelType { get; set; } // e.g., "Gasoline", "Diesel", "Electric"
    }
}

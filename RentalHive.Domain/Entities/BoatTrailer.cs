namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a boat available for rent. Inherits from RentalItem.
    /// </summary>
    public class Boat : RentalItem
    {
        public string BoatType { get; set; } // e.g., "Sailboat", "Motorboat", "Jet Ski"
        public int LengthInFeet { get; set; }
        public int Capacity { get; set; } // Number of people
        public bool RequiresLicense { get; set; }
    }
}

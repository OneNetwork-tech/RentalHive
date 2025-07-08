namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a minibus available for rent. Inherits from Car.
    /// </summary>
    public class Minibus : Car
    {
        // NumberOfSeats is already in the Car class and is the primary differentiator.
        public bool IsWheelchairAccessible { get; set; }
    }
}

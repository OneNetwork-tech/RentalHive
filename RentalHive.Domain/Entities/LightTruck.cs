namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a light truck available for rent. Inherits from Car.
    /// </summary>
    public class LightTruck : Car
    {
        public double PayloadCapacityInKg { get; set; }
        public bool HasTailLift { get; set; }
    }
}

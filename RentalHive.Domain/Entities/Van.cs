namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a van available for rent. Inherits from Car for some shared properties.
    /// </summary>
    public class Van : Car
    {
        public double LoadCapacityInCubicMeters { get; set; }
        public bool HasSlidingDoor { get; set; }
    }
}

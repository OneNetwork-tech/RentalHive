namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a generic trailer for rent. Inherits from RentalItem.
    /// </summary>
    public class Trailer : RentalItem
    {
        public double LoadCapacityInKg { get; set; }
        public double DeckWidthInMeters { get; set; }
        public double DeckLengthInMeters { get; set; }
        public bool HasBrakes { get; set; }
    }
}

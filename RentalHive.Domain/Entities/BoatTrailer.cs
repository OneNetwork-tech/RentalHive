namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a specialized trailer for transporting boats. Inherits from Trailer.
    /// This class must be public to be accessible by other projects.
    /// </summary>
    public class BoatTrailer : Trailer
    {
        public int MaxBoatLengthInFeet { get; set; }
        public bool IsSubmersible { get; set; }
    }
}

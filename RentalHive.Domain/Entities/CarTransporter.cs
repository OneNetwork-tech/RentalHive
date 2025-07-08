namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a specialized trailer for transporting cars. Inherits from Trailer.
    /// This class must be public to be accessible by other projects.
    /// </summary>
    public class CarTransporter : Trailer
    {
        public int NumberOfAxles { get; set; }
        public bool HasWinch { get; set; }
    }
}

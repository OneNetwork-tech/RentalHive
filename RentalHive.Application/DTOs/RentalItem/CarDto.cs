namespace RentalHive.Application.DTOs.RentalItem
{
    /// <summary>
    /// DTO for displaying detailed information about a Car.
    /// </summary>
    public class CarDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal DailyRate { get; set; }
        public int OwnerId { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public int NumberOfSeats { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
    }
}

// In RentalHive.Domain/Entities/Car.cs
using RentalHive.Domain.Entities;

public class Car : RentalItem
{
    public string Make { get; set; }
    public string Model { get; set; }
    public int Year { get; set; }
    public int NumberOfSeats { get; set; }
    public string TransmissionType { get; set; } // e.g., "Automatic", "Manual"
}
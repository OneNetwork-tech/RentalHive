// In RentalHive.Domain/Entities/Boat.cs
using RentalHive.Domain.Entities;

public class Boat : RentalItem
{
    public string BoatType { get; set; } // e.g., "Sailboat", "Motorboat"
    public int LengthInFeet { get; set; }
    public int Capacity { get; set; }
}
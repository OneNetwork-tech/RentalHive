// In RentalHive.Domain/Entities/Machine.cs
using RentalHive.Domain.Entities;

public class Machine : RentalItem
{
    public string MachineType { get; set; } // e.g., "Excavator", "Generator"
    public int Horsepower { get; set; }
    public double WeightInKg { get; set; }
}
namespace RentalHive.Domain.Entities;

/// <summary>Listing categories. Each maps to one entity type (table-per-hierarchy).</summary>
public enum ItemCategory
{
    Car,
    Van,
    Minibus,
    LightTruck,
    Trailer,
    CarTransporter,
    BoatTrailer,
    Boat,
    Machine,
}

/// <summary>Base class for everything that can be rented out.</summary>
public abstract class RentalItem
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Price per day in SEK.</summary>
    public decimal DailyRate { get; set; }

    public required string City { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ItemImage> Images { get; } = new();
    public List<Booking> Bookings { get; } = new();
    public List<Review> Reviews { get; } = new();

    public abstract ItemCategory Category { get; }
}

public class ItemImage
{
    public int Id { get; set; }
    public int RentalItemId { get; set; }
    public RentalItem? RentalItem { get; set; }

    /// <summary>Server-generated file name (random), never user supplied.</summary>
    public required string FileName { get; set; }

    public required string ContentType { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Car : RentalItem
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public int NumberOfSeats { get; set; }
    public Transmission Transmission { get; set; }
    public FuelType FuelType { get; set; }
    public override ItemCategory Category => ItemCategory.Car;
}

public enum Transmission
{
    Manual,
    Automatic,
}

public enum FuelType
{
    Petrol,
    Diesel,
    Electric,
    Hybrid,
    Gas,
}

public class Van : Car
{
    public double LoadCapacityInCubicMeters { get; set; }
    public bool HasSlidingDoor { get; set; }
    public override ItemCategory Category => ItemCategory.Van;
}

public class Minibus : Car
{
    public bool IsWheelchairAccessible { get; set; }
    public override ItemCategory Category => ItemCategory.Minibus;
}

public class LightTruck : Car
{
    public double PayloadCapacityInKg { get; set; }
    public bool HasTailLift { get; set; }
    public override ItemCategory Category => ItemCategory.LightTruck;
}

public class Trailer : RentalItem
{
    public double LoadCapacityInKg { get; set; }
    public double DeckWidthInMeters { get; set; }
    public double DeckLengthInMeters { get; set; }
    public bool HasBrakes { get; set; }
    public override ItemCategory Category => ItemCategory.Trailer;
}

public class CarTransporter : Trailer
{
    public int NumberOfAxles { get; set; }
    public bool HasWinch { get; set; }
    public override ItemCategory Category => ItemCategory.CarTransporter;
}

public class BoatTrailer : Trailer
{
    public int MaxBoatLengthInFeet { get; set; }
    public bool IsSubmersible { get; set; }
    public override ItemCategory Category => ItemCategory.BoatTrailer;
}

public class Boat : RentalItem
{
    public string BoatType { get; set; } = string.Empty;
    public int LengthInFeet { get; set; }
    public int Capacity { get; set; }
    public bool RequiresLicense { get; set; }
    public override ItemCategory Category => ItemCategory.Boat;
}

public class Machine : RentalItem
{
    public string MachineType { get; set; } = string.Empty;
    public int Horsepower { get; set; }
    public double WeightInKg { get; set; }
    public override ItemCategory Category => ItemCategory.Machine;
}

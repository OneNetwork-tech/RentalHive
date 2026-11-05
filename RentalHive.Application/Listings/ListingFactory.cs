using System.Globalization;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Application.Dtos;

namespace RentalHive.Application.Listings;

/// <summary>Maps <see cref="ListingInput"/> to the right entity type and validates category-specific fields.</summary>
public static class ListingFactory
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    public static IReadOnlyList<(ItemCategory Category, string Label)> Categories { get; } =
    [
        (ItemCategory.Car, "Car"),
        (ItemCategory.Van, "Van"),
        (ItemCategory.Minibus, "Minibus"),
        (ItemCategory.LightTruck, "Light truck"),
        (ItemCategory.Trailer, "Trailer"),
        (ItemCategory.CarTransporter, "Car transporter"),
        (ItemCategory.BoatTrailer, "Boat trailer"),
        (ItemCategory.Boat, "Boat"),
        (ItemCategory.Machine, "Machine"),
    ];

    public static string Label(ItemCategory c) => Categories.First(x => x.Category == c).Label;

    public static RentalItem Create(ListingInput input, int ownerId, DateTime now, int currentYear)
    {
        RentalItem item = input.Category switch
        {
            ItemCategory.Car => new Car { Name = "", City = "" },
            ItemCategory.Van => new Van { Name = "", City = "" },
            ItemCategory.Minibus => new Minibus { Name = "", City = "" },
            ItemCategory.LightTruck => new LightTruck { Name = "", City = "" },
            ItemCategory.Trailer => new Trailer { Name = "", City = "" },
            ItemCategory.CarTransporter => new CarTransporter { Name = "", City = "" },
            ItemCategory.BoatTrailer => new BoatTrailer { Name = "", City = "" },
            ItemCategory.Boat => new Boat { Name = "", City = "" },
            ItemCategory.Machine => new Machine { Name = "", City = "" },
            _ => throw new DomainException("Unknown category."),
        };
        item.OwnerId = ownerId;
        item.CreatedAt = now;
        Apply(input, item, now, currentYear);
        return item;
    }

    /// <summary>Validate and copy input onto an existing entity. The category cannot change.</summary>
    public static void Apply(ListingInput input, RentalItem item, DateTime now, int currentYear)
    {
        if (input.Category != item.Category) throw new DomainException("The category of a listing cannot be changed.");
        var errors = new List<string>();
        var name = (input.Name ?? "").Trim();
        if (name.Length is < 3 or > 120) errors.Add("The title must be 3–120 characters.");
        var city = (input.City ?? "").Trim();
        if (city.Length == 0) errors.Add("City is required.");
        if (input.DailyRate is < 1 or > 100000) errors.Add("The daily rate must be between 1 and 100 000 kr.");
        if (decimal.Round(input.DailyRate, 2) != input.DailyRate) errors.Add("The daily rate can have at most two decimals.");

        switch (item)
        {
            case Car car:
                ValidateVehicle(input, car, errors, currentYear);
                break;
            case Trailer trailer:
                ValidateTrailer(input, trailer, errors);
                break;
            case Boat boat:
                Require(input.BoatType, "Boat type", errors);
                Range(input.LengthInFeet, 1, 300, "Length (ft)", errors);
                Range(input.Capacity, 1, 200, "Passenger capacity", errors);
                boat.BoatType = Clean(input.BoatType);
                boat.LengthInFeet = input.LengthInFeet ?? 0;
                boat.Capacity = input.Capacity ?? 0;
                boat.RequiresLicense = input.RequiresLicense ?? false;
                break;
            case Machine machine:
                Require(input.MachineType, "Machine type", errors);
                Range(input.Horsepower, 0, 5000, "Horsepower", errors, required: false);
                Range(input.WeightInKg, 0, 100000, "Weight (kg)", errors, required: false);
                machine.MachineType = Clean(input.MachineType);
                machine.Horsepower = input.Horsepower ?? 0;
                machine.WeightInKg = input.WeightInKg ?? 0;
                break;
        }

        if (errors.Count > 0) throw new DomainException(string.Join(" ", errors));
        item.Name = name;
        item.City = city;
        item.Description = (input.Description ?? "").Trim();
        item.DailyRate = input.DailyRate;
        item.UpdatedAt = now;
    }

    private static void ValidateVehicle(ListingInput input, Car car, List<string> errors, int currentYear)
    {
        Require(input.Make, "Make", errors);
        Require(input.Model, "Model", errors);
        Range(input.Year, 1950, currentYear + 1, "Model year", errors);
        var (minSeats, maxSeats) = car switch
        {
            Minibus => (7, 20),
            LightTruck => (1, 9),
            Van => (1, 9),
            _ => (1, 9),
        };
        Range(input.NumberOfSeats, minSeats, maxSeats, "Seats", errors);
        if (input.Transmission is null) errors.Add("Transmission is required.");
        if (input.FuelType is null) errors.Add("Fuel type is required.");
        car.Make = Clean(input.Make);
        car.Model = Clean(input.Model);
        car.Year = input.Year ?? 0;
        car.NumberOfSeats = input.NumberOfSeats ?? 0;
        car.Transmission = input.Transmission ?? Transmission.Manual;
        car.FuelType = input.FuelType ?? FuelType.Petrol;
        switch (car)
        {
            case Van van:
                Range(input.LoadCapacityInCubicMeters, 0.5, 40, "Load volume (m³)", errors);
                van.LoadCapacityInCubicMeters = input.LoadCapacityInCubicMeters ?? 0;
                van.HasSlidingDoor = input.HasSlidingDoor ?? false;
                break;
            case Minibus bus:
                bus.IsWheelchairAccessible = input.IsWheelchairAccessible ?? false;
                break;
            case LightTruck truck:
                Range(input.PayloadCapacityInKg, 100, 3500, "Payload (kg)", errors);
                truck.PayloadCapacityInKg = input.PayloadCapacityInKg ?? 0;
                truck.HasTailLift = input.HasTailLift ?? false;
                break;
        }
    }

    private static void ValidateTrailer(ListingInput input, Trailer trailer, List<string> errors)
    {
        Range(input.LoadCapacityInKg, 50, 3500, "Load capacity (kg)", errors);
        Range(input.DeckWidthInMeters, 0.5, 3, "Deck width (m)", errors);
        Range(input.DeckLengthInMeters, 0.5, 12, "Deck length (m)", errors);
        trailer.LoadCapacityInKg = input.LoadCapacityInKg ?? 0;
        trailer.DeckWidthInMeters = input.DeckWidthInMeters ?? 0;
        trailer.DeckLengthInMeters = input.DeckLengthInMeters ?? 0;
        trailer.HasBrakes = input.HasBrakes ?? false;
        switch (trailer)
        {
            case CarTransporter ct:
                Range(input.NumberOfAxles, 1, 4, "Axles", errors);
                ct.NumberOfAxles = input.NumberOfAxles ?? 0;
                ct.HasWinch = input.HasWinch ?? false;
                break;
            case BoatTrailer bt:
                Range(input.MaxBoatLengthInFeet, 5, 60, "Max boat length (ft)", errors);
                bt.MaxBoatLengthInFeet = input.MaxBoatLengthInFeet ?? 0;
                bt.IsSubmersible = input.IsSubmersible ?? false;
                break;
        }
    }

    private static string Clean(string? s) => (s ?? "").Trim();

    private static void Require(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{label} is required.");
    }

    private static void Range(double? value, double min, double max, string label, List<string> errors, bool required = true)
    {
        if (value is null)
        {
            if (required) errors.Add($"{label} is required.");
            return;
        }
        if (value < min || value > max) errors.Add(string.Create(CultureInfo.InvariantCulture, $"{label} must be between {min} and {max}."));
    }

    private static void Range(int? value, int min, int max, string label, List<string> errors, bool required = true) =>
        Range(value is null ? null : (double?)value.Value, min, max, label, errors, required);

    /// <summary>Copy an entity back into an editable input model.</summary>
    public static ListingInput ToInput(RentalItem item)
    {
        var input = new ListingInput
        {
            Category = item.Category,
            Name = item.Name,
            Description = item.Description,
            DailyRate = item.DailyRate,
            City = item.City,
        };
        if (item is Car c)
        {
            input.Make = c.Make;
            input.Model = c.Model;
            input.Year = c.Year;
            input.NumberOfSeats = c.NumberOfSeats;
            input.Transmission = c.Transmission;
            input.FuelType = c.FuelType;
        }
        switch (item)
        {
            case Van v:
                input.LoadCapacityInCubicMeters = v.LoadCapacityInCubicMeters;
                input.HasSlidingDoor = v.HasSlidingDoor;
                break;
            case Minibus m:
                input.IsWheelchairAccessible = m.IsWheelchairAccessible;
                break;
            case LightTruck t:
                input.PayloadCapacityInKg = t.PayloadCapacityInKg;
                input.HasTailLift = t.HasTailLift;
                break;
            case Boat b:
                input.BoatType = b.BoatType;
                input.LengthInFeet = b.LengthInFeet;
                input.Capacity = b.Capacity;
                input.RequiresLicense = b.RequiresLicense;
                break;
            case Machine mc:
                input.MachineType = mc.MachineType;
                input.Horsepower = mc.Horsepower;
                input.WeightInKg = mc.WeightInKg;
                break;
        }
        if (item is Trailer tr)
        {
            input.LoadCapacityInKg = tr.LoadCapacityInKg;
            input.DeckWidthInMeters = tr.DeckWidthInMeters;
            input.DeckLengthInMeters = tr.DeckLengthInMeters;
            input.HasBrakes = tr.HasBrakes;
        }
        if (item is CarTransporter ctr)
        {
            input.NumberOfAxles = ctr.NumberOfAxles;
            input.HasWinch = ctr.HasWinch;
        }
        if (item is BoatTrailer btr)
        {
            input.MaxBoatLengthInFeet = btr.MaxBoatLengthInFeet;
            input.IsSubmersible = btr.IsSubmersible;
        }
        return input;
    }

    /// <summary>Short line shown on listing cards.</summary>
    public static string Subtitle(RentalItem item) => item switch
    {
        Car c => $"{c.Make} {c.Model} {c.Year} · {c.NumberOfSeats} seats · {c.Transmission}",
        Trailer t => string.Create(Sv, $"{t.LoadCapacityInKg:0} kg · {t.DeckLengthInMeters:0.#}×{t.DeckWidthInMeters:0.#} m"),
        Boat b => $"{b.BoatType} · {b.LengthInFeet} ft · {b.Capacity} persons",
        Machine m => m.MachineType,
        _ => Label(item.Category),
    };

    /// <summary>Specification table for the details page.</summary>
    public static IReadOnlyList<SpecDto> Specs(RentalItem item)
    {
        var s = new List<SpecDto> { new("Category", Label(item.Category)) };
        string yesNo(bool v) => v ? "Yes" : "No";
        if (item is Car c)
        {
            s.Add(new("Make & model", $"{c.Make} {c.Model}"));
            s.Add(new("Model year", c.Year.ToString(CultureInfo.InvariantCulture)));
            s.Add(new("Seats", c.NumberOfSeats.ToString(CultureInfo.InvariantCulture)));
            s.Add(new("Transmission", c.Transmission.ToString()));
            s.Add(new("Fuel", c.FuelType.ToString()));
        }
        switch (item)
        {
            case Van v:
                s.Add(new("Load volume", string.Create(Sv, $"{v.LoadCapacityInCubicMeters:0.#} m³")));
                s.Add(new("Sliding door", yesNo(v.HasSlidingDoor)));
                break;
            case Minibus m:
                s.Add(new("Wheelchair accessible", yesNo(m.IsWheelchairAccessible)));
                break;
            case LightTruck t:
                s.Add(new("Payload", string.Create(Sv, $"{t.PayloadCapacityInKg:0} kg")));
                s.Add(new("Tail lift", yesNo(t.HasTailLift)));
                break;
            case Boat b:
                s.Add(new("Type", b.BoatType));
                s.Add(new("Length", $"{b.LengthInFeet} ft"));
                s.Add(new("Capacity", $"{b.Capacity} persons"));
                s.Add(new("Boating licence required", yesNo(b.RequiresLicense)));
                break;
            case Machine mc:
                s.Add(new("Type", mc.MachineType));
                if (mc.Horsepower > 0) s.Add(new("Power", $"{mc.Horsepower} hp"));
                if (mc.WeightInKg > 0) s.Add(new("Weight", string.Create(Sv, $"{mc.WeightInKg:0} kg")));
                break;
        }
        if (item is Trailer tr)
        {
            s.Add(new("Load capacity", string.Create(Sv, $"{tr.LoadCapacityInKg:0} kg")));
            s.Add(new("Deck", string.Create(Sv, $"{tr.DeckLengthInMeters:0.##} × {tr.DeckWidthInMeters:0.##} m")));
            s.Add(new("Brakes", yesNo(tr.HasBrakes)));
        }
        if (item is CarTransporter ctr)
        {
            s.Add(new("Axles", ctr.NumberOfAxles.ToString(CultureInfo.InvariantCulture)));
            s.Add(new("Winch", yesNo(ctr.HasWinch)));
        }
        if (item is BoatTrailer btr)
        {
            s.Add(new("Max boat length", $"{btr.MaxBoatLengthInFeet} ft"));
            s.Add(new("Submersible", yesNo(btr.IsSubmersible)));
        }
        return s;
    }
}

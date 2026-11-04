using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using RentalHive.Application.Dtos;
using RentalHive.Application.Listings;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Security;
using RentalHive.Infrastructure.Storage;
using RentalHive.Infrastructure.Web;

namespace RentalHive.Tests;

public class PersonalNumberTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    [Theory]
    [InlineData("19811218-9876", "198112189876")]
    [InlineData("811218-9876", "198112189876")]
    [InlineData("198112189876", "198112189876")]
    [InlineData("121212-1212", "201212121212")]
    [InlineData("121212+1212", "191212121212")]
    public void Parses_and_normalises(string input, string expected)
    {
        Assert.True(PersonalNumber.TryParse(input, Today, out var p));
        Assert.Equal(expected, p!.Value);
    }

    [Theory]
    [InlineData("19811218-9877")] // check digit
    [InlineData("19811318-9876")] // month 13
    [InlineData("abc")]
    [InlineData("1981-12-18")]
    [InlineData("")]
    public void Rejects_invalid(string input) => Assert.False(PersonalNumber.TryParse(input, Today, out _));

    [Fact]
    public void Samordningsnummer_is_accepted()
    {
        // Day + 60; check digit computed for 701063-238x.
        var payload = "701063238";
        var check = Enumerable.Range(0, 10).First(d => PersonalNumber.LuhnValid(payload + d));
        Assert.True(PersonalNumber.TryParse($"{payload[..6]}-{payload[6..]}{check}", Today, out var p));
        Assert.Equal(new DateOnly(1970, 10, 3), p!.BirthDate);
    }

    [Fact]
    public void Age_and_masking()
    {
        PersonalNumber.TryParse("19811218-9876", Today, out var p);
        Assert.Equal(44, p!.AgeOn(Today));
        Assert.Equal("19811218-****", p.Masked);
        Assert.Equal("19811218-****", p.ToString());
    }
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("070-123 45 67", "+46701234567")]
    [InlineData("+46 70 123 45 67", "+46701234567")]
    [InlineData("0046701234567", "+46701234567")]
    [InlineData("08-123 456 78", "+46812345678")]
    public void Normalises_to_e164(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var n));
        Assert.Equal(expected, n);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("phone")]
    [InlineData("070-123<script>")]
    public void Rejects_invalid(string input) => Assert.False(PhoneNumber.TryNormalize(input, out _));
}

public class BookingRuleTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);
    private static readonly DateTime Now = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Car Item(int owner = 1) => new() { Id = 10, OwnerId = owner, Name = "Volvo", City = "Lund", DailyRate = 450m, IsActive = true };

    [Fact]
    public void Price_is_days_times_rate()
    {
        var b = Booking.Request(Item(), 2, Today.AddDays(2), Today.AddDays(5), "hi", Today, Now);
        Assert.Equal(3, b.Days);
        Assert.Equal(1350m, b.TotalCost);
        Assert.Equal(BookingStatus.Pending, b.Status);
    }

    [Fact]
    public void Period_rules()
    {
        Assert.Throws<DomainException>(() => Booking.ValidatePeriod(Today.AddDays(-1), Today.AddDays(2), Today));
        Assert.Throws<DomainException>(() => Booking.ValidatePeriod(Today.AddDays(3), Today.AddDays(3), Today));
        Assert.Throws<DomainException>(() => Booking.ValidatePeriod(Today, Today.AddDays(91), Today));
        Assert.Throws<DomainException>(() => Booking.ValidatePeriod(Today.AddDays(400), Today.AddDays(401), Today));
        Assert.Equal(1, Booking.ValidatePeriod(Today, Today.AddDays(1), Today));
    }

    [Fact]
    public void Owner_cannot_book_own_item()
    {
        var ex = Assert.Throws<DomainException>(() => Booking.Request(Item(owner: 2), 2, Today.AddDays(1), Today.AddDays(2), null, Today, Now));
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    [Fact]
    public void State_transitions_are_enforced()
    {
        var b = Booking.Request(Item(), 2, Today.AddDays(2), Today.AddDays(4), null, Today, Now);
        Assert.Equal(DomainErrorKind.Forbidden, Assert.Throws<DomainException>(() => b.Confirm(2, 1, Now)).Kind); // renter can't confirm
        b.Confirm(1, 1, Now);
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => b.Decline(1, 1, Now)).Kind);
        Assert.Throws<DomainException>(() => b.Complete(1, 1, Today, Now)); // before return date
        Assert.Throws<DomainException>(() => b.Cancel(2, 1, Today.AddDays(2), Now)); // rental started
        b.Complete(1, 1, Today.AddDays(4), Now);
        Assert.Equal(BookingStatus.Completed, b.Status);
    }

    [Fact]
    public void Overlap_is_half_open()
    {
        var b = new Booking { StartDate = Today.AddDays(1), EndDate = Today.AddDays(3) };
        Assert.True(b.Overlaps(Today.AddDays(2), Today.AddDays(4)));
        Assert.False(b.Overlaps(Today.AddDays(3), Today.AddDays(5))); // return day == next pick-up
        Assert.False(b.Overlaps(Today, Today.AddDays(1)));
    }
}

public class ListingFactoryTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Creates_the_right_subtype_with_validated_fields()
    {
        var item = ListingFactory.Create(new ListingInput
        {
            Category = ItemCategory.Van, Name = "VW Transporter", City = "Malmö", DailyRate = 700, Make = "VW", Model = "Transporter",
            Year = 2021, NumberOfSeats = 3, Transmission = Transmission.Manual, FuelType = FuelType.Diesel, LoadCapacityInCubicMeters = 6.7, HasSlidingDoor = true,
        }, 5, Now, 2026);
        var van = Assert.IsType<Van>(item);
        Assert.Equal(ItemCategory.Van, van.Category);
        Assert.True(van.HasSlidingDoor);
        Assert.Contains(ListingFactory.Specs(van), s => s.Label == "Load volume");
    }

    [Fact]
    public void Reports_all_missing_category_fields()
    {
        var ex = Assert.Throws<DomainException>(() => ListingFactory.Create(new ListingInput
        {
            Category = ItemCategory.Car, Name = "Car", City = "Lund", DailyRate = 300,
        }, 1, Now, 2026));
        Assert.Contains("Make is required", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Transmission is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_cannot_change_on_update()
    {
        var boat = ListingFactory.Create(new ListingInput { Category = ItemCategory.Boat, Name = "Ryds 488", City = "Vaxholm", DailyRate = 900, BoatType = "Motorboat", LengthInFeet = 16, Capacity = 5 }, 1, Now, 2026);
        var input = ListingFactory.ToInput(boat);
        input.Category = ItemCategory.Car;
        Assert.Throws<DomainException>(() => ListingFactory.Apply(input, boat, Now, 2026));
    }

    [Fact]
    public void Round_trips_through_input()
    {
        var input = new ListingInput
        {
            Category = ItemCategory.BoatTrailer, Name = "Båttrailer", City = "Uppsala", DailyRate = 250,
            LoadCapacityInKg = 1300, DeckWidthInMeters = 2, DeckLengthInMeters = 6, HasBrakes = true, MaxBoatLengthInFeet = 20, IsSubmersible = true,
        };
        var item = ListingFactory.Create(input, 1, Now, 2026);
        var back = ListingFactory.ToInput(item);
        Assert.Equal(20, back.MaxBoatLengthInFeet);
        Assert.True(back.IsSubmersible);
        Assert.True(back.HasBrakes);
    }
}

public class SecurityTests
{
    private static PersonalNumberProtector Protector(byte[]? key = null) =>
        new(Options.Create(new SecurityOptions { PersonalNumberKey = Convert.ToBase64String(key ?? RandomNumberGenerator.GetBytes(32)) }));

    [Fact]
    public void Personal_numbers_round_trip_and_hash_deterministically()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var p = Protector(key);
        var c1 = p.Encrypt("198112189876");
        var c2 = p.Encrypt("198112189876");
        Assert.NotEqual(c1, c2); // random nonce
        Assert.DoesNotContain("198112", c1, StringComparison.Ordinal);
        Assert.Equal("198112189876", p.Decrypt(c1));
        Assert.Equal(p.Hash("198112189876"), Protector(key).Hash("198112189876"));
        Assert.NotEqual(p.Hash("198112189876"), Protector().Hash("198112189876")); // keyed
    }

    [Fact]
    public void Tampered_cipher_is_rejected()
    {
        var p = Protector();
        var bytes = Convert.FromBase64String(p.Encrypt("198112189876"));
        bytes[^1] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Short_keys_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => new PersonalNumberProtector(Options.Create(new SecurityOptions { PersonalNumberKey = Convert.ToBase64String(new byte[16]) })));

    [Fact]
    public void Passwords_are_bcrypt_hashed()
    {
        var h = new BcryptPasswordHasher(Options.Create(new SecurityOptions { BcryptWorkFactor = 4 }));
        var hash = h.Hash("correct horse battery");
        Assert.StartsWith("$2", hash, StringComparison.Ordinal);
        Assert.True(h.Verify("correct horse battery", hash));
        Assert.False(h.Verify("wrong", hash));
        Assert.False(h.Verify("x", "not-a-hash"));
    }

    [Theory]
    [InlineData("/my/bookings", "/my/bookings")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData(null, "/")]
    public void Return_urls_must_be_local(string? input, string expected) => Assert.Equal(expected, UrlSafety.LocalOrRoot(input));

    [Fact]
    public void Images_are_identified_by_content()
    {
        Assert.Equal("image/png", LocalImageStore.Sniff([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0])?.ContentType);
        Assert.Equal("image/jpeg", LocalImageStore.Sniff([0xFF, 0xD8, 0xFF, 0xE0])?.ContentType);
        Assert.Null(LocalImageStore.Sniff("<html><script>"u8));
        Assert.True(LocalImageStore.IsValidName("0123456789abcdef0123456789abcdef.png"));
        Assert.False(LocalImageStore.IsValidName("../../etc/passwd"));
        Assert.False(LocalImageStore.IsValidName("0123456789abcdef0123456789abcdef.html"));
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Tests;

[Collection(ApiTestGroup.Name)]
public class AuthApiTests(ApiFactory api)
{
    [Fact]
    public async Task Register_then_login_with_email_personnummer_or_phone()
    {
        var u = await api.CreateUserAsync("anna");
        var anon = api.CreateClient();
        var shortPnr = u.PersonalNumber[2..]; // YYMMDD-NNNN form
        foreach (var id in new[] { u.Email.ToUpperInvariant(), shortPnr, u.PersonalNumber.Replace("-", "", StringComparison.Ordinal), u.Phone })
        {
            var res = await anon.PostAsJsonAsync("/api/auth/login", new { loginIdentifier = id, password = "a-long-password" });
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"Login with '{id}' failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        var bad = await anon.PostAsJsonAsync("/api/auth/login", new { loginIdentifier = u.Email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_phone_number_that_is_also_a_valid_personnummer()
    {
        // 0701234567-style numbers can pass the personnummer date and checksum rules.
        string phone;
        do
        {
            phone = ApiFactory.NewPhone();
        }
        while (!PersonalNumber.TryParse(phone, DateOnly.FromDateTime(DateTime.UtcNow), out _));

        var anon = api.CreateClient();
        var email = $"ambiguous-{Guid.NewGuid():N}@example.se";
        var reg = await anon.PostAsJsonAsync("/api/auth/register", new
        {
            personalNumber = ApiFactory.NewPersonalNumber(), firstName = "Amb", lastName = "Iguous", email, phoneNumber = phone, city = "Luleå",
            password = "a-long-password",
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var login = await anon.PostAsJsonAsync("/api/auth/login", new { loginIdentifier = phone, password = "a-long-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Registration_validates_identity_and_uniqueness()
    {
        var u = await api.CreateUserAsync();
        var anon = api.CreateClient();
        object Body(string pnr, string email, string phone) => new
        {
            personalNumber = pnr, firstName = "X", lastName = "Y", email, phoneNumber = phone, city = "Lund", password = "a-long-password",
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/auth/register", Body("19811218-9877", "x1@example.se", ApiFactory.NewPhone()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/auth/register", Body(ApiFactory.NewPersonalNumber(DateTime.UtcNow.Year - 10), "kid@example.se", ApiFactory.NewPhone()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsJsonAsync("/api/auth/register", Body(ApiFactory.NewPersonalNumber(), u.Email, ApiFactory.NewPhone()))).StatusCode);
        // Same personnummer written differently is still a duplicate.
        Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsJsonAsync("/api/auth/register", Body(u.PersonalNumber[2..], "other@example.se", ApiFactory.NewPhone()))).StatusCode);
    }

    [Fact]
    public async Task Personnummer_is_encrypted_at_rest_and_masked_in_profile()
    {
        var u = await api.CreateUserAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>();
        var row = await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        var digits = u.PersonalNumber.Replace("-", "", StringComparison.Ordinal);
        Assert.DoesNotContain(digits, row.PersonalNumberCipher, StringComparison.Ordinal);
        Assert.DoesNotContain(digits, row.PersonalNumberHash, StringComparison.Ordinal);
        var profile = await u.Client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal($"{digits[..8]}-****", profile.GetProperty("personalNumberMasked").GetString());
        Assert.DoesNotContain(digits[8..], profile.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures()
    {
        var u = await api.CreateUserAsync();
        var anon = api.CreateClient();
        for (var i = 0; i < 5; i++) await anon.PostAsJsonAsync("/api/auth/login", new { loginIdentifier = u.Email, password = "nope-nope-nope" });
        var res = await anon.PostAsJsonAsync("/api/auth/login", new { loginIdentifier = u.Email, password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("Too many", await res.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tokens_are_validated_and_revoked_on_password_change()
    {
        var u = await api.CreateUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await u.Client.GetAsync("/api/me")).StatusCode);

        var forged = api.CreateClient();
        var parts = u.Token.Split('.');
        forged.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{parts[0]}.{parts[1]}.{parts[2][..^4]}AAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/me")).StatusCode);
        var none = api.CreateClient();
        none.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.{parts[1]}.");
        Assert.Equal(HttpStatusCode.Unauthorized, (await none.GetAsync("/api/me")).StatusCode);

        var change = await u.Client.PostAsJsonAsync("/api/me/password", new { currentPassword = "a-long-password", newPassword = "an-even-longer-password" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        // The old token no longer works…
        Assert.Equal(HttpStatusCode.Unauthorized, (await u.Client.GetAsync("/api/me")).StatusCode);
        // …but the newly issued one does.
        var fresh = api.CreateClient();
        var token = (await change.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_users_cannot_write()
    {
        var anon = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/listings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/bookings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/bookings")).StatusCode);
    }
}

[Collection(ApiTestGroup.Name)]
public class MarketplaceApiTests(ApiFactory api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static object Car(string city = "Göteborg", decimal rate = 500) => new
    {
        category = "Car", name = "Volvo V60 med dragkrok", description = "Fin bil", dailyRate = rate, city,
        make = "Volvo", model = "V60", year = 2020, numberOfSeats = 5, transmission = "Automatic", fuelType = "Diesel",
    };

    private static async Task<int> CreateListingAsync(TestUser owner, object? body = null)
    {
        var res = await owner.Client.PostAsJsonAsync("/api/listings", body ?? Car(), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> RequestAsync(TestUser renter, int itemId, int fromDays, int toDays)
    {
        var res = await renter.Client.PostAsJsonAsync("/api/bookings", new
        {
            rentalItemId = itemId, startDate = Today.AddDays(fromDays).ToString("yyyy-MM-dd"), endDate = Today.AddDays(toDays).ToString("yyyy-MM-dd"), message = "Hej!",
        });
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement : default);
    }

    [Fact]
    public async Task Listing_validation_and_ownership()
    {
        var owner = await api.CreateUserAsync("owner");
        var other = await api.CreateUserAsync("other");
        var bad = await owner.Client.PostAsJsonAsync("/api/listings", new { category = "Car", name = "No details", dailyRate = 100, city = "Lund" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Make is required", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var id = await CreateListingAsync(owner);
        // Another user can neither edit nor delete it, and gets 404 (not 403) so ids can't be probed.
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PutAsJsonAsync($"/api/listings/{id}", Car(rate: 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.DeleteAsync($"/api/listings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PutAsJsonAsync($"/api/listings/{id}", Car(rate: 650))).StatusCode);

        var details = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/listings/{id}");
        Assert.Equal(650m, details.GetProperty("dailyRate").GetDecimal());
        Assert.Equal("owner T.", details.GetProperty("ownerName").GetString()); // first name + last initial only
        // Owner contact details and personnummer are never part of public listing data.
        var raw = details.GetRawText();
        Assert.DoesNotContain(owner.Email, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(owner.PersonalNumber[..8], raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_filters_by_category_city_price_and_availability()
    {
        var owner = await api.CreateUserAsync();
        var renter = await api.CreateUserAsync();
        var city = "Sökstad" + Guid.NewGuid().ToString("N")[..6];
        var cheap = await CreateListingAsync(owner, Car(city, 300));
        var pricey = await CreateListingAsync(owner, Car(city, 1500));
        await CreateListingAsync(owner, new
        {
            category = "Trailer", name = "Släpvagn 750 kg", dailyRate = 200, city,
            loadCapacityInKg = 750, deckWidthInMeters = 1.2, deckLengthInMeters = 2.5, hasBrakes = false,
        });
        var anon = api.CreateClient();
        var all = await anon.GetFromJsonAsync<JsonElement>($"/api/listings?city={Uri.EscapeDataString(city)}");
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        var cars = await anon.GetFromJsonAsync<JsonElement>($"/api/listings?city={Uri.EscapeDataString(city)}&category=Car&maxPrice=1000");
        Assert.Equal(cheap, cars.GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(1, cars.GetProperty("total").GetInt32());

        // Book the pricey car; it disappears from searches for those dates only.
        var (status, booking) = await RequestAsync(renter, pricey, 10, 13);
        Assert.Equal(HttpStatusCode.Created, status);
        await owner.Client.PostAsync($"/api/bookings/{booking.GetProperty("id").GetInt32()}/confirm", null);
        string Range(int a, int b) => $"&from={Today.AddDays(a):yyyy-MM-dd}&to={Today.AddDays(b):yyyy-MM-dd}";
        var during = await anon.GetFromJsonAsync<JsonElement>($"/api/listings?city={Uri.EscapeDataString(city)}&category=Car{Range(11, 12)}");
        Assert.Equal(1, during.GetProperty("total").GetInt32());
        var after = await anon.GetFromJsonAsync<JsonElement>($"/api/listings?city={Uri.EscapeDataString(city)}&category=Car{Range(13, 15)}");
        Assert.Equal(2, after.GetProperty("total").GetInt32());
        // LIKE wildcards in user input are escaped.
        Assert.Equal(0, (await anon.GetFromJsonAsync<JsonElement>("/api/listings?query=%25%25%25")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Booking_lifecycle_with_conflict_handling()
    {
        var owner = await api.CreateUserAsync("owner");
        var a = await api.CreateUserAsync("renterA");
        var b = await api.CreateUserAsync("renterB");
        var c = await api.CreateUserAsync("renterC");
        var item = await CreateListingAsync(owner);

        // Owners can't book their own items.
        Assert.Equal(HttpStatusCode.Forbidden, (await RequestAsync(owner, item, 5, 7)).Status);

        var quote = await a.Client.GetFromJsonAsync<JsonElement>($"/api/listings/{item}/quote?start={Today.AddDays(5):yyyy-MM-dd}&end={Today.AddDays(8):yyyy-MM-dd}");
        Assert.Equal(3, quote.GetProperty("days").GetInt32());
        Assert.Equal(1500m, quote.GetProperty("total").GetDecimal());

        var (s1, reqA) = await RequestAsync(a, item, 5, 8);
        var (s2, reqB) = await RequestAsync(b, item, 6, 9); // overlapping pending requests are allowed
        Assert.Equal(HttpStatusCode.Created, s1);
        Assert.Equal(HttpStatusCode.Created, s2);
        Assert.Equal("Pending", reqA.GetProperty("status").GetString());
        Assert.Equal(1500m, reqA.GetProperty("totalCost").GetDecimal());
        // Contact details stay hidden until the owner confirms.
        Assert.Equal(JsonValueKind.Null, reqA.GetProperty("counterpartEmail").ValueKind);

        var idA = reqA.GetProperty("id").GetInt32();
        var idB = reqB.GetProperty("id").GetInt32();
        // Renters and outsiders can't confirm; outsiders don't even see the booking.
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.PostAsync($"/api/bookings/{idA}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Client.PostAsync($"/api/bookings/{idA}/confirm", null)).StatusCode);

        var confirm = await owner.Client.PostAsync($"/api/bookings/{idA}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(a.Email.ToLowerInvariant(), confirmed.GetProperty("counterpartEmail").GetString()); // stored normalised

        // The overlapping request was declined automatically.
        var bList = await b.Client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.Equal("Declined", bList.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == idB).GetProperty("status").GetString());

        // New requests for those dates are refused; adjacent dates (return day = pick-up day) are fine.
        Assert.Equal(HttpStatusCode.Conflict, (await RequestAsync(c, item, 7, 10)).Status);
        Assert.Equal(HttpStatusCode.Created, (await RequestAsync(c, item, 8, 10)).Status);

        // The owner's view shares the renter's contact details for confirmed bookings only.
        var incoming = await owner.Client.GetFromJsonAsync<JsonElement>("/api/bookings/incoming");
        Assert.Equal(3, incoming.GetArrayLength());

        // Renter cancels before the start.
        var cancel = await a.Client.PostAsync($"/api/bookings/{idA}/cancel", null);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.PostAsync($"/api/bookings/{idA}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Database_prevents_overlapping_confirmed_bookings()
    {
        var owner = await api.CreateUserAsync();
        var renter = await api.CreateUserAsync();
        var item = await CreateListingAsync(owner);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>();
        Booking Make(int from, int to) => new()
        {
            RentalItemId = item, RenterId = renter.Id, StartDate = Today.AddDays(from), EndDate = Today.AddDays(to), Days = to - from,
            DailyRate = 500, TotalCost = 500 * (to - from), Status = BookingStatus.Confirmed, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Bookings.Add(Make(20, 25));
        await db.SaveChangesAsync();
        db.Bookings.Add(Make(24, 27));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(RentalHiveDbContext.NoOverlapConstraint, ex.InnerException?.Message ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Completed_rentals_can_be_reviewed_once()
    {
        var owner = await api.CreateUserAsync();
        var renter = await api.CreateUserAsync("reviewer");
        var item = await CreateListingAsync(owner);
        int bookingId;
        using (var scope = api.Services.CreateScope())
        {
            // A confirmed rental that has already ended (cannot be created through the API).
            var db = scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>();
            var bk = new Booking
            {
                RentalItemId = item, RenterId = renter.Id, StartDate = Today.AddDays(-5), EndDate = Today.AddDays(-2), Days = 3,
                DailyRate = 500, TotalCost = 1500, Status = BookingStatus.Confirmed, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.Bookings.Add(bk);
            await db.SaveChangesAsync();
            bookingId = bk.Id;
        }
        Assert.Equal(HttpStatusCode.Conflict, (await renter.Client.PostAsJsonAsync($"/api/bookings/{bookingId}/review", new { rating = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await renter.Client.PostAsync($"/api/bookings/{bookingId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/bookings/{bookingId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.PostAsJsonAsync($"/api/bookings/{bookingId}/review", new { rating = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await renter.Client.PostAsJsonAsync($"/api/bookings/{bookingId}/review", new { rating = 4, comment = "Bra bil!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await renter.Client.PostAsJsonAsync($"/api/bookings/{bookingId}/review", new { rating = 1 })).StatusCode);
        var details = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/listings/{item}");
        Assert.Equal(4.0, details.GetProperty("averageRating").GetDouble());
        Assert.Equal("Bra bil!", details.GetProperty("reviews")[0].GetProperty("comment").GetString());
        // Listings with booking history are deactivated rather than deleted.
        var del = await owner.Client.DeleteAsync($"/api/listings/{item}");
        Assert.False((await del.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deleted").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/listings/{item}")).StatusCode);
    }

    [Fact]
    public async Task Photos_are_validated_by_content_and_served_safely()
    {
        var owner = await api.CreateUserAsync();
        var item = await CreateListingAsync(owner);
        using (var html = new MultipartFormDataContent { { new ByteArrayContent("<script>alert(1)</script>"u8.ToArray()), "file", "cat.png" } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsync($"/api/listings/{item}/images", html)).StatusCode);
        }
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
        using var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "../../evil.html" } };
        var res = await owner.Client.PostAsync($"/api/listings/{item}/images", form);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var name = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fileName").GetString()!;
        Assert.Matches("^[a-f0-9]{32}\\.png$", name);
        var media = await api.CreateClient().GetAsync($"/media/{name}");
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal("image/png", media.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", media.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync("/media/..%2F..%2Fetc%2Fpasswd")).StatusCode);
    }

    [Fact]
    public async Task Health_and_security_headers()
    {
        var res = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", res.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }
}

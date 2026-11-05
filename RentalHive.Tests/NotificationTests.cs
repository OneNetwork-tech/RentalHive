using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RentalHive.Tests;

[Collection(ApiTestGroup.Name)]
public class NotificationTests(ApiFactory api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Phone numbers are stored in E.164 (070… → +4670…).</summary>
    private static string E164(string phone) => "+46" + phone[1..];

    private async Task<int> ListingAsync(TestUser owner, string name)
    {
        var res = await owner.Client.PostAsJsonAsync("/api/listings", new
        {
            category = "Car", name, description = "Fin bil", dailyRate = 250, city = "Västerås",
            make = "Volvo", model = "V60", year = 2020, numberOfSeats = 5, transmission = "Automatic", fuelType = "Diesel",
        }, ApiFactory.Json);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> RequestAsync(TestUser renter, int item, int from, int to, string message = "Hej! Kan jag hämta kl 9?")
    {
        var res = await renter.Client.PostAsJsonAsync("/api/bookings", new
        {
            rentalItemId = item, startDate = Today.AddDays(from).ToString("yyyy-MM-dd"), endDate = Today.AddDays(to).ToString("yyyy-MM-dd"), message,
        });
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Each_booking_change_emails_the_other_party()
    {
        var owner = await api.CreateUserAsync("Olle");
        var anna = await api.CreateUserAsync("Anna");
        var bert = await api.CreateUserAsync("Bert");
        var name = "Volvo " + Guid.NewGuid().ToString("N")[..6];
        var item = await ListingAsync(owner, name);

        // Request: the owner hears about it, with dates, price, message and a link.
        var a = await RequestAsync(anna, item, 3, 5);
        var b = await RequestAsync(bert, item, 4, 6);
        var request = await api.WaitForEmailAsync(owner.Email, $"New booking request: {name}", "Anna T.");
        Assert.Contains("Anna T.", request.Body, StringComparison.Ordinal);
        Assert.Contains("2 days, 500 kr", request.Body, StringComparison.Ordinal);
        Assert.Contains("Kan jag hämta kl 9?", request.Body, StringComparison.Ordinal);
        Assert.Contains("https://rentalhive.test/my/requests", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(E164(anna.Phone), request.Body, StringComparison.Ordinal); // no contact details before confirmation

        // Confirm: the renter gets the owner's contact details; the overlapping request is told it's unavailable.
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/bookings/{a}/confirm", null)).StatusCode);
        var confirmed = await api.WaitForEmailAsync(anna.Email, $"Booking confirmed: {name}");
        Assert.Contains(E164(owner.Phone), confirmed.Body, StringComparison.Ordinal);
        var unavailable = await api.WaitForEmailAsync(bert.Email, $"Not available: {name}");
        Assert.Contains("declined", unavailable.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(E164(owner.Phone), unavailable.Body, StringComparison.Ordinal);

        // Renter cancels: the owner is told.
        Assert.Equal(HttpStatusCode.OK, (await anna.Client.PostAsync($"/api/bookings/{a}/cancel", null)).StatusCode);
        var cancelled = await api.WaitForEmailAsync(owner.Email, $"Booking cancelled: {name}");
        Assert.Contains("Anna T. cancelled", cancelled.Body, StringComparison.Ordinal);
        Assert.False(api.HasEmail(owner.Email, $"Booking confirmed: {name}"));
        _ = b;
    }

    [Fact]
    public async Task Declining_emails_the_renter()
    {
        var owner = await api.CreateUserAsync("Olle");
        var renter = await api.CreateUserAsync("Cecilia");
        var name = "Volvo " + Guid.NewGuid().ToString("N")[..6];
        var item = await ListingAsync(owner, name);
        var id = await RequestAsync(renter, item, 2, 3, message: "");

        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/bookings/{id}/decline", null)).StatusCode);
        var declined = await api.WaitForEmailAsync(renter.Email, $"Request declined: {name}");
        Assert.Contains("https://rentalhive.test/search", declined.Body, StringComparison.Ordinal);
        var request = await api.WaitForEmailAsync(owner.Email, $"New booking request: {name}");
        Assert.DoesNotContain("Their message", request.Body, StringComparison.Ordinal);
    }
}

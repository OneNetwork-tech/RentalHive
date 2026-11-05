using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Tests;

[Collection(ApiTestGroup.Name)]
public class ModerationTests(ApiFactory api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<T> WithService<T>(Func<IModerationService, Task<T>> action)
    {
        using var scope = api.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IModerationService>());
    }

    private Task<bool> Moderate(Func<IModerationService, Task> action) => WithService(async s =>
    {
        await action(s);
        return true;
    });

    private async Task<TestUser> AdminAsync()
    {
        var admin = await api.CreateUserAsync("Mod");
        Assert.True(await WithService(s => s.SetAdminAsync(admin.Email, true)));
        return admin;
    }

    private async Task<int> ListingAsync(TestUser owner, string name)
    {
        var res = await owner.Client.PostAsJsonAsync("/api/listings", new
        {
            category = "Car", name, description = "Fin bil", dailyRate = 400, city = "Örebro",
            make = "Saab", model = "9-3", year = 2010, numberOfSeats = 5, transmission = "Manual", fuelType = "Petrol",
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<int> RequestAsync(TestUser renter, int item, int from, int to)
    {
        var res = await renter.Client.PostAsJsonAsync("/api/bookings", new
        {
            rentalItemId = item, startDate = Today.AddDays(from).ToString("yyyy-MM-dd"), endDate = Today.AddDays(to).ToString("yyyy-MM-dd"), message = "",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<BookingStatus> StatusOf(int bookingId)
    {
        using var scope = api.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>().Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId)).Status;
    }

    [Fact]
    public async Task Members_can_report_listings_but_not_their_own()
    {
        var owner = await api.CreateUserAsync("Olle");
        var reporter = await api.CreateUserAsync("Rita");
        var item = await ListingAsync(owner, "Saab " + Guid.NewGuid().ToString("N")[..6]);

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "My own listing" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.Client.PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.Client.PostAsJsonAsync("/api/listings/999999/report", new { reason = "Does not exist" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "Anonymous report" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await reporter.Client.PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "The photos are from another website" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reporter.Client.PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "Updated: photos are stolen" })).StatusCode);

        var admin = await AdminAsync();
        var reports = (await WithService(s => s.GetOpenReportsAsync(admin.Id))).Where(r => r.ItemId == item).ToList();
        var report = Assert.Single(reports); // reporting twice updates the open report
        Assert.Equal("Updated: photos are stolen", report.Reason);
        Assert.Equal(owner.Email.ToLowerInvariant(), report.OwnerEmail);

        await Moderate(s => s.DismissReportAsync(admin.Id, report.Id));
        Assert.DoesNotContain(await WithService(s => s.GetOpenReportsAsync(admin.Id)), r => r.ItemId == item);
    }

    [Fact]
    public async Task Only_moderators_can_moderate()
    {
        var member = await api.CreateUserAsync("Max");
        var ex = await Assert.ThrowsAsync<DomainException>(() => WithService(s => s.GetOpenReportsAsync(member.Id)));
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
        await Assert.ThrowsAsync<DomainException>(() => Moderate(s => s.SuspendAsync(member.Id, member.Id, "Trying to suspend")));

        // Revoking moderator rights takes effect immediately (checked against the database).
        var admin = await AdminAsync();
        await WithService(s => s.GetOpenReportsAsync(admin.Id));
        await WithService(s => s.SetAdminAsync(admin.Email, false));
        await Assert.ThrowsAsync<DomainException>(() => WithService(s => s.GetOpenReportsAsync(admin.Id)));
    }

    [Fact]
    public async Task Removing_a_listing_hides_it_for_good_and_cancels_pending_requests()
    {
        var owner = await api.CreateUserAsync("Olle");
        var renter = await api.CreateUserAsync("Rut");
        var name = "Saab " + Guid.NewGuid().ToString("N")[..6];
        var item = await ListingAsync(owner, name);
        var booking = await RequestAsync(renter, item, 5, 7);
        await renter.Client.PostAsJsonAsync($"/api/listings/{item}/report", new { reason = "This is a scam listing" });

        var admin = await AdminAsync();
        await Moderate(s => s.RemoveListingAsync(admin.Id, item, "Scam: asks for payment up front"));

        Assert.Equal(HttpStatusCode.NotFound, (await renter.Client.GetAsync($"/api/listings/{item}")).StatusCode);
        Assert.Equal(BookingStatus.Cancelled, await StatusOf(booking));
        Assert.DoesNotContain(await WithService(s => s.GetOpenReportsAsync(admin.Id)), r => r.ItemId == item);

        // The owner can't put it back.
        var reactivate = await owner.Client.PutAsJsonAsync($"/api/listings/{item}/active", new { active = true });
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
        var mine = await owner.Client.GetFromJsonAsync<JsonElement>("/api/listings/mine");
        var listed = mine.EnumerateArray().Single(l => l.GetProperty("id").GetInt32() == item);
        Assert.Equal("Scam: asks for payment up front", listed.GetProperty("removalReason").GetString());

        var ownerMail = await api.WaitForEmailAsync(owner.Email, $"Your listing was removed: {name}");
        Assert.Contains("Scam: asks for payment up front", ownerMail.Body, StringComparison.Ordinal);
        await api.WaitForEmailAsync(renter.Email, $"Not available: {name}");
    }

    [Fact]
    public async Task Suspension_blocks_sign_in_revokes_tokens_and_hides_listings()
    {
        var owner = await api.CreateUserAsync("Sune");
        var renter = await api.CreateUserAsync("Rut");
        var other = await api.CreateUserAsync("Ove");
        var name = "Saab " + Guid.NewGuid().ToString("N")[..6];
        var item = await ListingAsync(owner, name);
        var otherItem = await ListingAsync(other, "Volvo " + Guid.NewGuid().ToString("N")[..6]);
        var incoming = await RequestAsync(renter, item, 3, 4);   // someone wants the suspended member's item
        var outgoing = await RequestAsync(owner, otherItem, 3, 4); // the suspended member's own request

        var admin = await AdminAsync();
        var self = await Assert.ThrowsAsync<DomainException>(() => Moderate(s => s.SuspendAsync(admin.Id, admin.Id, "Suspend myself")));
        Assert.Equal(DomainErrorKind.Conflict, self.Kind);
        await Moderate(s => s.SuspendAsync(admin.Id, owner.Id, "Repeated no-shows and abuse"));

        // Existing token stops working at once; a new sign-in is refused with a clear message.
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.GetAsync("/api/me")).StatusCode);
        var login = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { loginIdentifier = owner.Email, password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        var wrong = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { loginIdentifier = owner.Email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode); // suspension isn't revealed without the password

        Assert.Equal(HttpStatusCode.NotFound, (await renter.Client.GetAsync($"/api/listings/{item}")).StatusCode);
        Assert.Equal(BookingStatus.Cancelled, await StatusOf(incoming));
        Assert.Equal(BookingStatus.Cancelled, await StatusOf(outgoing));
        await api.WaitForEmailAsync(owner.Email, "Your RentalHive account has been suspended");
        await api.WaitForEmailAsync(renter.Email, $"Not available: {name}");

        var found = await WithService(s => s.FindMembersAsync(admin.Id, owner.Email.ToUpperInvariant()));
        Assert.NotNull(Assert.Single(found).SuspendedAt);
        Assert.Contains(await WithService(s => s.FindMembersAsync(admin.Id, "")), m => m.Id == owner.Id); // empty search lists suspended members
        Assert.Single(await WithService(s => s.FindMembersAsync(admin.Id, owner.PersonalNumber)));

        // Lifting the suspension lets them sign in; listings stay inactive until they reactivate them.
        await Moderate(s => s.UnsuspendAsync(admin.Id, owner.Id));
        var again = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { loginIdentifier = owner.Email, password = "a-long-password" });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await renter.Client.GetAsync($"/api/listings/{item}")).StatusCode);
    }

    [Fact]
    public async Task Moderators_cannot_be_suspended()
    {
        var admin = await AdminAsync();
        var otherAdmin = await AdminAsync();
        var ex = await Assert.ThrowsAsync<DomainException>(() => Moderate(s => s.SuspendAsync(admin.Id, otherAdmin.Id, "Disagreement")));
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
        Assert.False(await WithService(s => s.SetAdminAsync("nobody-" + Guid.NewGuid().ToString("N") + "@example.se", true)));
    }
}

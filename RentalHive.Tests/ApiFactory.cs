using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RentalHive.Domain.Common;

namespace RentalHive.Tests;

/// <summary>
/// Hosts the real API against a real PostgreSQL database (TEST_DATABASE_URL, or a local
/// default). The schema is dropped and re-migrated once per test run.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? "Host=127.0.0.1;Port=5432;Database=rentalhive_test;Username=rentalhive;Password=rentalhive";

    public string ImageDir { get; } = Path.Combine(Path.GetTempPath(), "rentalhive-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        // Fresh schema for every run (the API applies migrations on startup).
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", conn);
        cmd.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting is applied before Program reads configuration (minimal hosting).
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:RentalHive"] = ConnectionString,
            ["Jwt:Key"] = "integration-test-signing-key-that-is-long-enough-123",
            ["Security:PersonalNumberKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Security:BcryptWorkFactor"] = "4",
            ["Storage:ImageDirectory"] = ImageDir,
            ["RateLimits:AuthPerMinute"] = "1000",
            ["App:PublicUrl"] = "https://rentalhive.test",
        };
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
    }

    /// <summary>Waits for the background sender to handle an email to <paramref name="to"/> matching <paramref name="subject"/>.</summary>
    public async Task<RentalHive.Application.Contracts.EmailMessage> WaitForEmailAsync(string to, string subject, string? bodyContains = null)
    {
        var outbox = Services.GetRequiredService<RentalHive.Infrastructure.Email.EmailOutbox>();
        for (var i = 0; i < 100; i++)
        {
            var hit = outbox.All.LastOrDefault(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.Subject.Contains(subject, StringComparison.Ordinal)
                                                  && (bodyContains is null || m.Body.Contains(bodyContains, StringComparison.Ordinal)));
            if (hit is not null) return hit;
            await Task.Delay(50);
        }

        throw new Xunit.Sdk.XunitException($"No email '{subject}' to {to}. Sent: {string.Join("; ", outbox.All.Select(m => m.To + ": " + m.Subject))}");
    }

    public bool HasEmail(string to, string subject) =>
        Services.GetRequiredService<RentalHive.Infrastructure.Email.EmailOutbox>().All
            .Any(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.Subject.Contains(subject, StringComparison.Ordinal));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Generates a unique, valid personnummer for an adult born in the given year.</summary>
    public static string NewPersonalNumber(int year = 1985)
    {
        while (true)
        {
            var month = RandomNumberGenerator.GetInt32(1, 13);
            var day = RandomNumberGenerator.GetInt32(1, 29);
            var serial = RandomNumberGenerator.GetInt32(0, 1000);
            var payload = $"{year % 100:00}{month:00}{day:00}{serial:000}";
            for (var check = 0; check < 10; check++)
            {
                if (PersonalNumber.LuhnValid(payload + check)) return $"{year}{month:00}{day:00}-{serial:000}{check}";
            }
        }
    }

    private static int _phoneCounter = RandomNumberGenerator.GetInt32(1_000_000, 9_000_000);

    public static string NewPhone() => $"070{Interlocked.Increment(ref _phoneCounter):0000000}";

    /// <summary>Register a user and return an authenticated client plus identifiers.</summary>
    public async Task<TestUser> CreateUserAsync(string? name = null)
    {
        var client = CreateClient();
        var email = $"{name ?? "user"}-{Guid.NewGuid():N}@example.se";
        var pnr = NewPersonalNumber();
        var phone = NewPhone();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            personalNumber = pnr, firstName = name ?? "Test", lastName = "Testsson", email, phoneNumber = phone, city = "Stockholm",
            password = "a-long-password",
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = body.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new TestUser(client, body.GetProperty("user").GetProperty("id").GetInt32(), email, pnr, phone, token);
    }
}

public sealed record TestUser(HttpClient Client, int Id, string Email, string PersonalNumber, string Phone, string Token);

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

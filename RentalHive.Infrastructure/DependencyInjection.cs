using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RentalHive.Application.Contracts;
using RentalHive.Infrastructure.Email;
using RentalHive.Infrastructure.Persistence;
using RentalHive.Infrastructure.Security;
using RentalHive.Infrastructure.Services;
using RentalHive.Infrastructure.Storage;

namespace RentalHive.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("RentalHive");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:RentalHive is not configured (set ConnectionStrings__RentalHive).");

        services.AddDbContext<RentalHiveDbContext>(o => o
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddOptions<SecurityOptions>().Bind(config.GetSection(SecurityOptions.Section))
            .Validate(o => !string.IsNullOrWhiteSpace(o.PersonalNumberKey), "Security:PersonalNumberKey is required (base64, 32 bytes).")
            .ValidateOnStart();
        services.AddOptions<StorageOptions>().Bind(config.GetSection(StorageOptions.Section));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IPersonalNumberProtector, PersonalNumberProtector>();
        services.AddSingleton<IImageStore, LocalImageStore>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IModerationService, ModerationService>();

        // Email: queued and sent in the background (logged only when Email:Host is empty).
        services.AddOptions<EmailOptions>().Bind(config.GetSection(EmailOptions.Section));
        services.AddOptions<AppOptions>().Bind(config.GetSection(AppOptions.Section));
        services.AddSingleton<EmailOutbox>();
        services.AddSingleton<BackgroundEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<BackgroundEmailSender>());
        services.AddHostedService<EmailDispatcher>();
        services.AddScoped<BookingNotifier>();
        return services;
    }

    /// <summary>Apply pending migrations (EF Core takes a database lock, so concurrent starts are safe).</summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RentalHive.Migrations");
        var pending = (await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }
}

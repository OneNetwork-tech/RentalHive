using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RentalHive.Infrastructure.Persistence.DatabaseContext
{
    /// <summary>
    /// A factory for creating derived DbContext instances, used by EF Core design-time tools.
    /// </summary>
    public class RentalHiveDbContextFactory : IDesignTimeDbContextFactory<RentalHiveDbContext>
    {
        public RentalHiveDbContext CreateDbContext(string[] args)
        {
            // --- Start Diagnostic Code ---
            string currentDirectory = Directory.GetCurrentDirectory();
            string appSettingsPath = Path.Combine(currentDirectory, "appsettings.json");

            // Check if the appsettings.json file exists at the expected path.
            if (!File.Exists(appSettingsPath))
            {
                // If it doesn't exist, throw a very specific error.
                throw new FileNotFoundException(
                    $"The appsettings.json file was not found at the expected path: '{appSettingsPath}'. " +
                    $"Please ensure the file exists in the 'RentalHive.Infrastructure' project and its 'Copy to Output Directory' property is set to 'Copy if newer'.");
            }
            // --- End Diagnostic Code ---

            // Build configuration from the appsettings.json file.
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Read the connection string from the configuration.
            var connectionString = configuration.GetConnectionString("RentalHiveConnectionString");

            // --- Start Diagnostic Code ---
            // Check if the connection string was actually found in the file.
            if (string.IsNullOrEmpty(connectionString))
            {
                // If not found, throw a very specific error.
                throw new InvalidOperationException(
                    "The connection string 'RentalHiveConnectionString' could not be found inside the appsettings.json file. " +
                    "Please check that the file has the correct format: { \"ConnectionStrings\": { \"RentalHiveConnectionString\": \"...\" } }");
            }
            // --- End Diagnostic Code ---

            var optionsBuilder = new DbContextOptionsBuilder<RentalHiveDbContext>();

            // Configure the DbContext to use PostgreSQL with the retrieved connection string.
            optionsBuilder.UseNpgsql(connectionString);

            // Return the configured DbContext instance.
            return new RentalHiveDbContext(optionsBuilder.Options);
        }
    }
}


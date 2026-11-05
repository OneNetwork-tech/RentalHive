using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RentalHive.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef`. Reads the connection string from RENTALHIVE_DB (never from a committed file).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RentalHiveDbContext>
{
    public RentalHiveDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("RENTALHIVE_DB")
                 ?? "Host=localhost;Database=rentalhive_design;Username=postgres";
        var options = new DbContextOptionsBuilder<RentalHiveDbContext>()
            .UseNpgsql(cs)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RentalHiveDbContext(options);
    }
}

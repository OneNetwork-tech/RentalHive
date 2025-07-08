using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Infrastructure.Persistence.DatabaseContext;
using RentalHive.Infrastructure.Persistence.Repositories;

namespace RentalHive.Infrastructure
{
    /// <summary>
    /// Extension method to register all Infrastructure layer services.
    /// </summary>
    public static class InfrastructureServicesRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure the DbContext to use PostgreSQL
            services.AddDbContext<RentalHiveDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("RentalHiveConnectionString")));

            // Register repositories
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IRentalItemRepository, RentalItemRepository>();

            return services;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalHive.Application.Contracts.Identity;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Infrastructure.Identity;
using RentalHive.Infrastructure.Persistence.DatabaseContext;
using RentalHive.Infrastructure.Persistence.Repositories;

namespace RentalHive.Infrastructure
{
    public static class InfrastructureServicesRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<RentalHiveDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("RentalHiveConnectionString")));

            // Register repositories
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IRentalItemRepository, RentalItemRepository>();

            // Register new identity services
            services.AddTransient<IPasswordHasher, PasswordHasher>();
            services.AddTransient<IJwtTokenGenerator, JwtTokenGenerator>();

            return services;
        }
    }
}

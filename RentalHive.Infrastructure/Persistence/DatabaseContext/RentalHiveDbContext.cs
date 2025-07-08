using Microsoft.EntityFrameworkCore;
using RentalHive.Domain.Entities;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace RentalHive.Infrastructure.Persistence.DatabaseContext
{
    /// <summary>
    /// The main DbContext for the application, configured to use Entity Framework Core.
    /// It defines the DbSets for our entities, which represent tables in the database.
    /// </summary>
    public class RentalHiveDbContext : DbContext
    {
        public RentalHiveDbContext(DbContextOptions<RentalHiveDbContext> options) : base(options)
        {
        }

        // Define DbSets for each of your root entities.
        public DbSet<User> Users { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<RentalItem> RentalItems { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Boat> Boats { get; set; }
        public DbSet<Machine> Machines { get; set; }
        // Add other specific types if you need to query them directly.

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This line automatically scans the assembly for all IEntityTypeConfiguration classes
            // and applies them. This keeps our DbContext clean.
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Example of a manual configuration if needed:
            modelBuilder.Entity<User>(entity =>
            {
                // Ensure PersonalIdentityNumber is unique.
                entity.HasIndex(u => u.PersonalIdentityNumber).IsUnique();
                entity.Property(u => u.PersonalIdentityNumber).IsRequired();
                entity.Property(u => u.Email).IsRequired();
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                // Configure the relationship for the renter
                entity.HasOne(b => b.Renter)
                      .WithMany(u => u.Bookings)
                      .HasForeignKey(b => b.RenterId)
                      .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a user with bookings

                // Configure the relationship for the rental item
                entity.HasOne(b => b.RentalItem)
                      .WithMany(i => i.Bookings)
                      .HasForeignKey(b => b.RentalItemId)
                      .OnDelete(DeleteBehavior.Restrict); // Prevent deleting an item with bookings
            });
        }
    }
}

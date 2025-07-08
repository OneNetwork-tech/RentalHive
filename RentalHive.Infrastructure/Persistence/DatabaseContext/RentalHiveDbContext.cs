using Microsoft.EntityFrameworkCore;
using RentalHive.Domain.Entities;
using System.Reflection;

namespace RentalHive.Infrastructure.Persistence.DatabaseContext
{
    public class RentalHiveDbContext : DbContext
    {
        public RentalHiveDbContext(DbContextOptions<RentalHiveDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<RentalItem> RentalItems { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Boat> Boats { get; set; }
        public DbSet<Machine> Machines { get; set; }
        public DbSet<Van> Vans { get; set; }
        public DbSet<Minibus> Minibuses { get; set; }
        public DbSet<LightTruck> LightTrucks { get; set; }
        public DbSet<Trailer> Trailers { get; set; }
        public DbSet<CarTransporter> CarTransporters { get; set; }
        public DbSet<BoatTrailer> BoatTrailers { get; set; }
       

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.PersonalIdentityNumber).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.PersonalIdentityNumber).IsRequired();
                entity.Property(u => u.Email).IsRequired();
                entity.Property(u => u.PhoneNumber).IsRequired(false);
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasOne(b => b.Renter)
                      .WithMany(u => u.Bookings)
                      .HasForeignKey(b => b.RenterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.RentalItem)
                      .WithMany(i => i.Bookings)
                      .HasForeignKey(b => b.RentalItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}

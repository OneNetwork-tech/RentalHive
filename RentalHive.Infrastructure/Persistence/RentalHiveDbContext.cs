using Microsoft.EntityFrameworkCore;
using RentalHive.Domain.Entities;

namespace RentalHive.Infrastructure.Persistence;

public class RentalHiveDbContext(DbContextOptions<RentalHiveDbContext> options) : DbContext(options)
{
    /// <summary>Name of the exclusion constraint that prevents overlapping confirmed bookings.</summary>
    public const string NoOverlapConstraint = "ex_bookings_no_overlap";

    public DbSet<User> Users => Set<User>();
    public DbSet<RentalItem> RentalItems => Set<RentalItem>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("btree_gist");

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.PhoneNumber).IsUnique();
            e.HasIndex(u => u.PersonalNumberHash).IsUnique();
            e.Property(u => u.Email).HasMaxLength(254);
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.PersonalNumberHash).HasMaxLength(64);
            e.Property(u => u.PersonalNumberCipher).HasMaxLength(200);
            e.Property(u => u.Address).HasMaxLength(200);
            e.Property(u => u.City).HasMaxLength(100);
            e.Property(u => u.PasswordHash).HasMaxLength(100);
            e.Property(u => u.SuspensionReason).HasMaxLength(500);
            e.Ignore(u => u.DisplayName);
            e.Ignore(u => u.IsSuspended);
        });

        b.Entity<RentalItem>(e =>
        {
            e.HasDiscriminator<string>("item_type")
                .HasValue<Car>(nameof(ItemCategory.Car))
                .HasValue<Van>(nameof(ItemCategory.Van))
                .HasValue<Minibus>(nameof(ItemCategory.Minibus))
                .HasValue<LightTruck>(nameof(ItemCategory.LightTruck))
                .HasValue<Trailer>(nameof(ItemCategory.Trailer))
                .HasValue<CarTransporter>(nameof(ItemCategory.CarTransporter))
                .HasValue<BoatTrailer>(nameof(ItemCategory.BoatTrailer))
                .HasValue<Boat>(nameof(ItemCategory.Boat))
                .HasValue<Machine>(nameof(ItemCategory.Machine));
            e.Property("item_type").HasMaxLength(30);
            e.Ignore(i => i.Category);
            e.Property(i => i.Name).HasMaxLength(120);
            e.Property(i => i.Description).HasMaxLength(4000);
            e.Property(i => i.City).HasMaxLength(100);
            e.Property(i => i.DailyRate).HasPrecision(10, 2);
            e.Property(i => i.RemovalReason).HasMaxLength(500);
            e.HasOne(i => i.Owner).WithMany(u => u.OwnedItems).HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(i => i.Images).WithOne(img => img.RentalItem).HasForeignKey(img => img.RentalItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex("item_type", nameof(RentalItem.IsActive));
            e.HasIndex(i => i.City);
            e.HasIndex(i => i.OwnerId);
        });

        // Shared columns for subtypes (TPH) so e.g. Van and Car use the same "make" column.
        b.Entity<Car>(e =>
        {
            e.Property(c => c.Make).HasMaxLength(60);
            e.Property(c => c.Model).HasMaxLength(60);
            e.Property(c => c.Transmission).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.FuelType).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<Boat>().Property(x => x.BoatType).HasMaxLength(60);
        b.Entity<Machine>().Property(x => x.MachineType).HasMaxLength(60);

        b.Entity<ItemImage>(e =>
        {
            e.Property(i => i.FileName).HasMaxLength(80);
            e.Property(i => i.ContentType).HasMaxLength(40);
            e.HasIndex(i => i.FileName).IsUnique();
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.DailyRate).HasPrecision(10, 2);
            e.Property(x => x.TotalCost).HasPrecision(12, 2);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.Version).IsRowVersion(); // maps to PostgreSQL xmin
            e.HasOne(x => x.RentalItem).WithMany(i => i.Bookings).HasForeignKey(x => x.RentalItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Renter).WithMany(u => u.Bookings).HasForeignKey(x => x.RenterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.RentalItemId, x.Status });
            e.HasIndex(x => x.RenterId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_bookings_period", "end_date > start_date");
                t.HasCheckConstraint("ck_bookings_days", "days = end_date - start_date");
            });
        });

        b.Entity<Review>(e =>
        {
            e.Property(r => r.Comment).HasMaxLength(2000);
            e.HasIndex(r => r.BookingId).IsUnique();
            e.HasIndex(r => r.RentalItemId);
            e.HasOne(r => r.Booking).WithOne(x => x.Review).HasForeignKey<Review>(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.RentalItem).WithMany(i => i.Reviews).HasForeignKey(r => r.RentalItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Author).WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));
        });

        b.Entity<ListingReport>(e =>
        {
            e.Property(r => r.Reason).HasMaxLength(ListingReport.MaxReasonLength);
            e.Property(r => r.Resolution).HasMaxLength(500);
            e.HasOne(r => r.RentalItem).WithMany().HasForeignKey(r => r.RentalItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.ResolvedAt);
            // One open report per member and listing.
            e.HasIndex(r => new { r.RentalItemId, r.ReporterId }).IsUnique().HasFilter("resolved_at IS NULL");
        });
    }
}

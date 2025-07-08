using Microsoft.EntityFrameworkCore;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.DatabaseContext;

namespace RentalHive.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implements the IBookingRepository interface with EF Core specific logic.
    /// </summary>
    public class BookingRepository : GenericRepository<Booking>, IBookingRepository
    {
        public BookingRepository(RentalHiveDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(int userId)
        {
            return await _context.Bookings
                .AsNoTracking()
                .Where(b => b.RenterId == userId)
                .Include(b => b.RentalItem) // Also load the related rental item info
                .ToListAsync();
        }
    }
}

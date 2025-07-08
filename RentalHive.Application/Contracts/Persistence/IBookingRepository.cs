using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Persistence
{
    /// <summary>
    /// Defines repository operations for Booking entities.
    /// </summary>
    public interface IBookingRepository : IGenericRepository<Booking>
    {
        Task<IReadOnlyList<Booking>> GetBookingsByUserIdAsync(int userId);
    }
}

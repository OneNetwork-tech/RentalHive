using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Persistence
{
    /// <summary>
    /// Defines repository operations for RentalItem entities.
    /// It inherits common data access methods from IGenericRepository.
    /// </summary>
    public interface IRentalItemRepository : IGenericRepository<RentalItem>
    {
        // This is the place to add any custom data access methods
        // that are specific to RentalItems in the future.
        // For example:
        // Task<IReadOnlyList<RentalItem>> GetAvailableItemsAsync(DateTime startDate, DateTime endDate);
    }
}

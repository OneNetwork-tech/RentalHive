using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Persistence
{
    /// <summary>
    /// Defines repository operations specific to RentalItem entities.
    /// Inherits generic operations and allows for extension with rental item-specific methods.
    /// </summary>
    public interface IRentalItemRepository : IGenericRepository<RentalItem>
    {
        // Add custom methods for RentalItem here if needed in the future.
    }
}

using RentalHive.Application.Contracts.Persistence;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.DatabaseContext;
// Ensure the correct namespace for IRentalItemRepository is included
// If IRentalItemRepository is in a different namespace, add the using directive here

namespace RentalHive.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implements the IRentalItemRepository interface.
    /// </summary>
    public class RentalItemRepository : GenericRepository<RentalItem>, IRentalItemRepository
    {
        public RentalItemRepository(RentalHiveDbContext context) : base(context)
        {
        }

        // Custom methods for rental items can be added here later.
    }
}

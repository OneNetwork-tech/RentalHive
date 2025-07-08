using RentalHive.Application.Contracts.Persistence;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.DatabaseContext;

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

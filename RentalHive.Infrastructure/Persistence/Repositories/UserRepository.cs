using Microsoft.EntityFrameworkCore;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.DatabaseContext;

namespace RentalHive.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implements the IUserRepository interface with EF Core specific logic.
    /// </summary>
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(RentalHiveDbContext context) : base(context)
        {
        }

        public async Task<User> GetUserByPersonalIdentityNumberAsync(string personalIdentityNumber)
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PersonalIdentityNumber == personalIdentityNumber);
        }

        public async Task<bool> IsPersonalIdentityNumberUnique(string personalIdentityNumber)
        {
            return !await _context.Users.AnyAsync(u => u.PersonalIdentityNumber == personalIdentityNumber);
        }
    }
}

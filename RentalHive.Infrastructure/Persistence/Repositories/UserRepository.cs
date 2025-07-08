using Microsoft.EntityFrameworkCore;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.DatabaseContext;

namespace RentalHive.Infrastructure.Persistence.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(RentalHiveDbContext context) : base(context)
        {
        }

        public async Task<User> GetUserByIdentifierAsync(string identifier)
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PersonalIdentityNumber == identifier || u.Email == identifier || u.PhoneNumber == identifier);
        }

        public async Task<bool> IsEmailUnique(string email)
        {
            return !await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<bool> IsPersonalIdentityNumberUnique(string personalIdentityNumber)
        {
            return !await _context.Users.AnyAsync(u => u.PersonalIdentityNumber == personalIdentityNumber);
        }
    }
}

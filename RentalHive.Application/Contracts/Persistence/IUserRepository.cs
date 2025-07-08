using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Persistence
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User> GetUserByIdentifierAsync(string identifier);
        Task<bool> IsPersonalIdentityNumberUnique(string personalIdentityNumber);
        Task<bool> IsEmailUnique(string email);
    }
}

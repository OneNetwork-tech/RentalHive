using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Persistence
{
    /// <summary>
    /// Defines repository operations specific to the User entity.
    /// Inherits generic operations and adds user-specific methods.
    /// </summary>
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User> GetUserByPersonalIdentityNumberAsync(string personalIdentityNumber);
        Task<bool> IsPersonalIdentityNumberUnique(string personalIdentityNumber);
    }
}

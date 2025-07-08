namespace RentalHive.Application.Contracts.Persistence
{
    /// <summary>
    /// Defines a generic repository with common data access operations.
    /// This provides a standard contract for all entity-specific repositories.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public interface IGenericRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}

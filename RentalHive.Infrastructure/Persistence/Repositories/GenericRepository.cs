using Microsoft.EntityFrameworkCore;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Infrastructure.Persistence.DatabaseContext;

namespace RentalHive.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Provides a generic implementation for the IGenericRepository interface.
    /// This class handles common CRUD operations using EF Core.
    /// </summary>
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly RentalHiveDbContext _context;

        public GenericRepository(RentalHiveDbContext context)
        {
            _context = context;
        }

        public async Task<T> AddAsync(T entity)
        {
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(T entity)
        {
            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id);
        }

        public async Task UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}

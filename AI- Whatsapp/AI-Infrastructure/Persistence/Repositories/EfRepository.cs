using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class EfRepository<T> : IRepository<T>
    where T : Entity<Guid>
{
    protected readonly PlatformDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(PlatformDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IReadOnlyList<T>> ListAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageIndex = 0,
        int pageSize = 50,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        if (pageSize > 0)
        {
            query = query.Skip(pageIndex * pageSize).Take(pageSize);
        }

        var results = await query.ToListAsync();
        return results.AsReadOnly();
    }

    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return await query.FirstOrDefaultAsync(predicate);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteByIdAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }

    public virtual async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

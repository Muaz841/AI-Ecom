using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IRepository<T> where T : Entity<Guid>, ITenantEntity
{
    Task<T?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<T>> ListAllAsync();

    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int pageIndex = 0,
        int pageSize = 50,
        params Expression<Func<T, object>>[] includes);

    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includes);

    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);

    Task UpdateAsync(T entity);

    Task DeleteAsync(T entity);

    Task DeleteByIdAsync(Guid id);

    Task SaveChangesAsync();
}

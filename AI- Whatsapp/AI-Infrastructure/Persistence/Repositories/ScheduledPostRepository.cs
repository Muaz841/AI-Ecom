using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class ScheduledPostRepository : EfRepository<ScheduledPost>
{
    public ScheduledPostRepository(PlatformDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetReadyToPublishAsync(DateTime nowUtc)
    {
        var posts = await _dbSet
            .Where(p => (p.Status == PostStatus.Pending || p.Status == PostStatus.Approved) && p.ScheduledFor <= nowUtc)
            .OrderBy(p => p.ScheduledFor)
            .ToListAsync();

        return posts.AsReadOnly();
    }
}

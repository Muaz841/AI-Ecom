using System;

namespace EcomAI.Platform.Business.Entities;

public abstract class Entity<TKey>
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;
}

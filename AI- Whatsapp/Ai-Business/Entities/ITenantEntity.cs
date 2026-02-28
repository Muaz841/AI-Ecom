using System;

namespace EcomAI.Platform.Business.Entities;

public interface ITenantEntity
{
    Guid? TenantId { get; }
}

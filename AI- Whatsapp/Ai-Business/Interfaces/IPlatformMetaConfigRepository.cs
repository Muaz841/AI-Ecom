using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IPlatformMetaConfigRepository
{
    Task<PlatformMetaConfig?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlatformMetaConfig config, CancellationToken cancellationToken = default);
}

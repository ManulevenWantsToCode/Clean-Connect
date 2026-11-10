using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Repositories;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(PlatformSettings settings, CancellationToken cancellationToken);
}

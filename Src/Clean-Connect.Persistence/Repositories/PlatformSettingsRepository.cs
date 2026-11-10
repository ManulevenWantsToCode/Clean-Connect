using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories;

public class PlatformSettingsRepository(ApplicationDbContext db) : IPlatformSettingsRepository
{
    public async Task<PlatformSettings> GetAsync(CancellationToken cancellationToken) =>
        await db.PlatformSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
        ?? new PlatformSettings();

    public async Task SaveAsync(PlatformSettings settings, CancellationToken cancellationToken)
    {
        settings.Id = 1;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        var existing = await db.PlatformSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (existing == null) await db.PlatformSettings.AddAsync(settings, cancellationToken);
        else
        {
            db.Entry(existing).CurrentValues.SetValues(settings);
            db.Entry(existing).Property(x => x.Id).IsModified = false;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}

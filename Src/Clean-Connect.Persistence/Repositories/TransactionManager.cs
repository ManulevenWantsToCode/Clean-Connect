using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Infrastructure.Context;

namespace Clean_Connect.Persistence.Repositories;

public sealed class TransactionManager(ApplicationDbContext db) : ITransactionManager
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await action(cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

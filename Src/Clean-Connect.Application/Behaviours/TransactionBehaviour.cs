using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Application.Command.BookingCommand;
using MediatR;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clean_Connect.Application.Behaviours
{
    public class TransactionBehaviour<TRequest, TResponse>
          : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public TransactionBehaviour(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Booking creation owns its short database transaction so it can roll back
            // before translating PostgreSQL idempotency and exclusion conflicts.
            if (request is CreateBookingCommand)
                return await next();

            // If already in transaction, just continue (prevents nesting issues)
            if (_dbContext.Database.CurrentTransaction != null)
            {
                return await next();
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var response = await next();

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return response;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}

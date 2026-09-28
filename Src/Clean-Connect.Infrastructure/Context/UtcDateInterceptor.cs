using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clean_Connect.Infrastructure.Context
{
    public class UtcDateInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            NormalizeDates(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            NormalizeDates(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void NormalizeDates(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            foreach (var entry in context.ChangeTracker.Entries())
            {
                foreach (var property in entry.Properties)
                {
                    if (property.CurrentValue is not DateTime dateTime)
                    {
                        continue;
                    }

                    var utcValue = dateTime.Kind switch
                    {
                        DateTimeKind.Utc => dateTime,
                        DateTimeKind.Local => dateTime.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    };

                    if (utcValue.Kind != dateTime.Kind || utcValue.Ticks != dateTime.Ticks)
                    {
                        property.CurrentValue = utcValue.AddTicks(1);
                    }
                    property.CurrentValue = utcValue;
                }
            }
        }
    }
}
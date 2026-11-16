using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories;

public sealed class AuditTrailWriter(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) : IAuditTrailWriter
{
    public async Task RecordLoginAsync(Guid? accountId, bool succeeded, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.Items["X-Correlation-ID"] as string;
        await using var auditContext = new ApplicationDbContext(options, httpContextAccessor);
        auditContext.AuditRecords.Add(AuditRecord.CreateAuthenticationEvent(DateTime.UtcNow, accountId,
            succeeded, correlationId));
        await auditContext.SaveChangesAsync(cancellationToken);
    }
}

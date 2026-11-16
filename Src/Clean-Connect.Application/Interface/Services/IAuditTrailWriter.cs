namespace Clean_Connect.Application.Interface.Services;

public interface IAuditTrailWriter
{
    Task RecordLoginAsync(Guid? accountId, bool succeeded, CancellationToken cancellationToken);
}

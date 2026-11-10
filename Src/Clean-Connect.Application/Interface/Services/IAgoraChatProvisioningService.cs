namespace Clean_Connect.Application.Interface.Services
{
    public interface IAgoraChatProvisioningService
    {
        Task EnsureUsersExistAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);
    }
}

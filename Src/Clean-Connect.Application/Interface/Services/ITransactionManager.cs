namespace Clean_Connect.Application.Interface.Services;

public interface ITransactionManager
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}

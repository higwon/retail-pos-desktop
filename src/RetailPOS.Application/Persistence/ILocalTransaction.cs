namespace RetailPOS.Application.Persistence;

public interface ILocalTransaction
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

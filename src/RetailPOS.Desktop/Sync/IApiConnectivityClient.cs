using System.Net.Http;

namespace RetailPOS.Desktop.Sync;

public interface IApiConnectivityClient
{
    Task<ApiConnectivitySnapshot> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class HttpApiConnectivityClient(
    HttpClient httpClient,
    TimeProvider timeProvider) : IApiConnectivityClient
{
    public async Task<ApiConnectivitySnapshot> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                "api/health",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.IsSuccessStatusCode
                ? Online()
                : Offline($"Http{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Offline(exception.GetType().Name);
        }
    }

    private ApiConnectivitySnapshot Online() =>
        new(ApiConnectivityStatus.Online, timeProvider.GetUtcNow(), null);

    private ApiConnectivitySnapshot Offline(string failureKind) =>
        new(ApiConnectivityStatus.Offline, timeProvider.GetUtcNow(), failureKind);
}

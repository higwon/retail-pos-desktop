using System.Net.Http.Json;
using System.Net;
using RetailPOS.Application.Orders;

namespace RetailPOS.Infrastructure.Sync;

public sealed class HttpOrderUploadClient(HttpClient httpClient) : IOrderUploadClient
{
    public async Task<OrderUploadResult> UploadAsync(
        OrderUploadPayload payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/orders",
            payload,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new OrderUploadConflictException("Order upload API returned an idempotency conflict.");
        }

        response.EnsureSuccessStatusCode();

        var uploadResponse = await response.Content.ReadFromJsonAsync<OrderUploadResponseDto>(
            cancellationToken);

        if (uploadResponse is null)
        {
            throw new InvalidOperationException("Order upload API returned an empty response.");
        }

        return new OrderUploadResult(
            uploadResponse.ServerOrderId,
            uploadResponse.OrderNumber,
            uploadResponse.SyncStatus);
    }

    private sealed record OrderUploadResponseDto(
        Guid ServerOrderId,
        string OrderNumber,
        string SyncStatus);
}

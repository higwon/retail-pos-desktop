namespace RetailPOS.Api;

public sealed record HealthResponse(
    string Status,
    DateTimeOffset ServerTime);

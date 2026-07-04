namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class SyncQueueEntity
{
    public Guid Id { get; set; }
    public required string ItemType { get; set; }
    public Guid AggregateId { get; set; }
    public string? PayloadJson { get; set; }
    public string? ReferenceKey { get; set; }
    public int Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LastErrorSummary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

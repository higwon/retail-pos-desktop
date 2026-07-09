namespace RetailPOS.Desktop.Sync;

public sealed class BackgroundOrderSyncOptions
{
    public const string SectionName = "SyncScheduler";

    public bool Enabled { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 5;
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 10;
}

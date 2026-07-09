namespace RetailPOS.Desktop.Sync;

public sealed class ApiConnectivityMonitorOptions
{
    public const string SectionName = "ApiConnectivity";

    public bool Enabled { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 2;
    public int IntervalSeconds { get; set; } = 30;
    public bool TriggerSyncOnReconnect { get; set; } = true;
}

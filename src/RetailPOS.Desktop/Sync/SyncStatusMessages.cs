using RetailPOS.Application.Sync;

namespace RetailPOS.Desktop.Sync;

public sealed record SyncStatusChangedMessage(string Reason);

public sealed record OrderSyncRunCompletedMessage(OrderSyncRunResult Result);

public sealed record ApiConnectivityChangedMessage(
    ApiConnectivitySnapshot Previous,
    ApiConnectivitySnapshot Current);

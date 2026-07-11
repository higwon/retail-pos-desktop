namespace RetailPOS.Application.Devices;

public enum DeviceAvailability { Available, Unavailable, Disabled }
public enum DeviceReadiness { Ready, Busy, Attention, Unknown }

public sealed record DeviceStatusSnapshot(
    string DeviceId,
    string DisplayName,
    DeviceAvailability Availability,
    DeviceReadiness Readiness,
    string Detail,
    DateTimeOffset LastChangedAtUtc);

public sealed record DeviceStatusOverview(IReadOnlyList<DeviceStatusSnapshot> Devices)
{
    public int AttentionCount => Devices.Count(device =>
        device.Availability != DeviceAvailability.Available ||
        device.Readiness != DeviceReadiness.Ready);

    public string Summary => AttentionCount == 0
        ? "Devices: Ready"
        : $"Devices: {AttentionCount:N0} Attention";
}

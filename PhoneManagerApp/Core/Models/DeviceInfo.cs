namespace PhoneManagerApp.Core.Models;

/// <summary>
/// Represents key information about a connected Android device.
/// This model is populated by the DeviceManager and displayed in the UI panels.
/// </summary>
public class DeviceInfo
{
    public string DeviceName { get; set; } = "Unknown";
    public string Serial { get; set; } = "—";
    public string IpAddress { get; set; } = "—";
    public string? ExtraIp { get; set; } = null; // 🟦 Tailscale IP (optional)

    // 📱 Battery
    public string BatteryLevel { get; set; } = "—";
    public bool IsCharging { get; set; }


    // 📶 Wi-Fi
    public string WifiSignal { get; set; } = "—";

    // 💾 Storage
    public string StorageUsage { get; set; } = "—";
    public string DiskStatsRaw { get; set; } = string.Empty;

    // 🕒 Metadata
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
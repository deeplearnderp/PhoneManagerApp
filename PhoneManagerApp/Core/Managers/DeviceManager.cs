using System.Diagnostics;
using System.Text.RegularExpressions;
using PhoneManagerApp.Core.Models;
using SharpAdbClient;

namespace PhoneManagerApp.Core.Managers;

/// <summary>
/// Coordinates ADB operations and converts raw output into a structured DeviceInfo model.
/// </summary>
public class DeviceManager
{
    private readonly AdbConnector _adbConnector;

    public DeviceManager(AdbConnector adbConnector)
    {
        _adbConnector = adbConnector;
    }

    /// <summary>
    /// Collects all current stats (battery, Wi-Fi, storage, etc.) and returns a DeviceInfo model.
    /// </summary>
    public async Task<DeviceInfo?> GetDeviceStatsAsync()
    {
        if (!_adbConnector.IsConnected)
        {
            Debug.WriteLine("⚠️ No connected device.");
            return null;
        }

        try
        {
            var client = new AdbClient();
            var device = _adbConnector.GetConnectedDevice();
            if (device == null) return null;

            // Run ADB commands
            var batteryRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys battery");
            var wifiRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys wifi | grep RSSI");
            var storageRaw = await ExecuteAdbCommandAsync(client, device,
                "df /data /storage/emulated/0 | grep -E '/data|/storage/emulated/0'");
            var diskStatsRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys diskstats");

            // Get both IPs
            var (wifiIp, tailscaleIp) = await TryGetDeviceIpsAsync(client, device);

            // Parse key data
            string batteryLevel = ParseBatteryLevel(batteryRaw);
            bool isCharging = ParseIsCharging(batteryRaw);
            string wifiSignal = ParseWifiSignal(wifiRaw);
            string storageUsage = ParseStorageUsage(storageRaw);

            // Build device info
            var info = new DeviceInfo
            {
                DeviceName = device.Model ?? "Unknown",
                Serial = device.Serial ?? "—",
                IpAddress = wifiIp,
                ExtraIp = tailscaleIp,
                BatteryLevel = batteryLevel,
                WifiSignal = wifiSignal,
                StorageUsage = storageUsage,
                DiskStatsRaw = diskStatsRaw,
                LastUpdated = DateTime.Now,
                IsCharging = isCharging
            };

            return info;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error reading device info: {ex.Message}");
            return null;
        }
    }

    // ======================================================
    // 🔧 Helper Methods
    // ======================================================

    private async Task<string> ExecuteAdbCommandAsync(AdbClient client, DeviceData device, string command)
    {
        var receiver = new ConsoleOutputReceiver();
        await Task.Run(() => client.ExecuteRemoteCommand(command, device, receiver));
        return receiver.ToString().Trim();
    }

    private async Task<(string WifiIp, string? TailscaleIp)> TryGetDeviceIpsAsync(AdbClient client, DeviceData device)
    {
        string wifiIp = "—";
        string? tailscaleIp = null;

        try
        {
            // Wi-Fi (wlan0)
            var wifiRaw = await ExecuteAdbCommandAsync(client, device, "ip -o -4 addr show wlan0 | grep 'inet '");
            var wifiMatch = Regex.Match(wifiRaw, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
            if (wifiMatch.Success)
            {
                wifiIp = wifiMatch.Groups[1].Value;
                Debug.WriteLine($"📶 Wi-Fi IP: {wifiIp}");
            }

            // Try to detect Tailscale (supports both tailscale0 and tun0)
            var tsRaw = await ExecuteAdbCommandAsync(client, device,
                "ip -o -4 addr show tailscale0 2>/dev/null || ip -o -4 addr show tun0 2>/dev/null");
            var tsMatch = Regex.Match(tsRaw, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
            if (tsMatch.Success)
            {
                tailscaleIp = tsMatch.Groups[1].Value;
                Debug.WriteLine($"🔷 Tailscale IP: {tailscaleIp}");
            }
            else
            {
                Debug.WriteLine("⚠️ No Tailscale or tun0 interface found.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ IP fetch error: {ex.Message}");
        }

        return (wifiIp, tailscaleIp);
    }

    private string ParseBatteryLevel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        var match = Regex.Match(raw, @"level[:=]\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? $"{match.Groups[1].Value}%" : "—";
    }

    private bool ParseIsCharging(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var match = Regex.Match(raw, @"status:\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int status))
        {
            // Android: 2 = charging, 3 = discharging, 5 = full
            return status == 2;
        }
        return false;
    }

    private string ParseWifiSignal(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        var match = Regex.Match(raw, @"RSSI[:=]\s*(-?\d+)", RegexOptions.IgnoreCase);
        if (!match.Success) return "—";

        var rssi = int.Parse(match.Groups[1].Value);
        var strength = rssi switch
        {
            >= -50 => "Excellent 📶📶📶📶",
            >= -60 => "Good 📶📶📶",
            >= -70 => "Fair 📶📶",
            >= -80 => "Weak 📶",
            _ => "Very Weak ❌"
        };
        return $"{strength} ({rssi} dBm)";
    }

    private string ParseStorageUsage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        var match = Regex.Match(raw, @"(\d+)%", RegexOptions.Multiline);
        return match.Success ? $"{match.Groups[1].Value}% used" : "—";
    }
}

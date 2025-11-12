using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoneManagerApp.Core.Models;
using SharpAdbClient;

namespace PhoneManagerApp.Core.Managers
{
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
                string batteryRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys battery | grep level");
                string wifiRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys wifi | grep RSSI");
                string storageRaw = await ExecuteAdbCommandAsync(client, device, "df /data /storage/emulated/0 | grep -E '/data|/storage/emulated/0'");
                string diskStatsRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys diskstats");

                // Parse and build model
                var info = new DeviceInfo
                {
                    DeviceName = device.Model ?? "Unknown",
                    Serial = device.Serial ?? "—",
                    IpAddress = await TryGetDeviceIpAsync(client, device),
                    BatteryLevel = ParseBatteryLevel(batteryRaw),
                    WifiSignal = ParseWifiSignal(wifiRaw),
                    StorageUsage = ParseStorageUsage(storageRaw),
                    DiskStatsRaw = diskStatsRaw,
                    LastUpdated = DateTime.Now
                };

                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Error reading device info: {ex.Message}");
                return null;
            }
        }

        // ========================
        // 🔧 Helper Methods
        // ========================

        private async Task<string> ExecuteAdbCommandAsync(AdbClient client, DeviceData device, string command)
        {
            var receiver = new ConsoleOutputReceiver();
            await Task.Run(() => client.ExecuteRemoteCommand(command, device, receiver));
            return receiver.ToString().Trim();
        }

        private async Task<string> TryGetDeviceIpAsync(AdbClient client, DeviceData device)
        {
            try
            {
                string ipRaw = await ExecuteAdbCommandAsync(client, device, "ip addr show wlan0 | grep 'inet '");
                var match = Regex.Match(ipRaw, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
                return match.Success ? match.Groups[1].Value : "—";
            }
            catch
            {
                return "—";
            }
        }

        private string ParseBatteryLevel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            var match = Regex.Match(raw, @"level[:=]\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups[1].Value}%" : "—";
        }

        private string ParseWifiSignal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            var match = Regex.Match(raw, @"RSSI[:=]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "—";

            int rssi = int.Parse(match.Groups[1].Value);
            string strength = rssi switch
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
}

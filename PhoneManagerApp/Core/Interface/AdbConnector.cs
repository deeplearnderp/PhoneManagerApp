using System.Diagnostics;
using PhoneManagerApp.Core.Interface;
using SharpAdbClient;

namespace PhoneManagerApp.Core;

public class AdbConnector : IDeviceConnector
{
    private readonly string _adbPath = @"C:\Users\coryn\Downloads\platform-tools-latest-windows\platform-tools\adb.exe";
    private AdbClient? _client;

    private DeviceData? _device;
    public string Name => "Android ADB Connector";
    public bool IsConnected { get; private set; }

    public async Task<bool> ConnectAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(_adbPath))
                {
                    Debug.WriteLine($"❌ ADB not found at: {_adbPath}");
                    return false;
                }

                var adbDir = Path.GetDirectoryName(_adbPath)!;
                Environment.CurrentDirectory = adbDir;

                var adbProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _adbPath,
                        Arguments = "start-server",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = adbDir
                    }
                };

                adbProcess.Start();
                adbProcess.WaitForExit(2000);

                var server = new AdbServer();
                server.StartServer(_adbPath, false);
                Thread.Sleep(800);

                _client = new AdbClient();
                var devices = _client.GetDevices();

                if (devices.Count == 0)
                {
                    Debug.WriteLine("⚠️ No devices found.");
                    return false;
                }

                _device = devices[0];
                IsConnected = true;
                Debug.WriteLine($"✅ Connected to {_device.Model} ({_device.Serial})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Connection error: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<IEnumerable<string>> GetFilesAsync(string? path = null)
    {
        return await Task.Run(() =>
        {
            var result = new List<string>
            {
                "⚙️  File explorer not implemented yet.",
                "Use ADB Control Mode for now."
            };
            return (IEnumerable<string>)result;
        });
    }

    public async Task<IEnumerable<string>> GetPackagesAsync()
    {
        return await Task.Run(() =>
        {
            var packages = new List<string>();

            if (!IsConnected || _client == null || _device == null)
            {
                packages.Add("⚠️ Device not connected.");
                return packages;
            }

            try
            {
                Debug.WriteLine("📦 Fetching installed packages...");
                var receiver = new ConsoleOutputReceiver();
                _client.ExecuteRemoteCommand("pm list packages", _device, receiver);
                foreach (var line in receiver.ToString().Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        packages.Add(line.Trim());
            }
            catch (Exception ex)
            {
                packages.Add($"❌ Error: {ex.Message}");
            }

            return packages;
        });
    }

    public DeviceData? GetConnectedDevice()
    {
        return _device;
    }

    public async Task<bool> ToggleNotificationsAsync(bool enabled)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (_device == null)
                    return false;

                var command = enabled
                    ? "settings put global heads_up_notifications_enabled 1"
                    : "settings put global heads_up_notifications_enabled 0";

                _client.ExecuteRemoteCommand(command, _device, null);
                Debug.WriteLine($"Notifications {(enabled ? "enabled" : "disabled")}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling notifications: {ex.Message}");
                return false;
            }
        });
    }
}
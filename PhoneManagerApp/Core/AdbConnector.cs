// Version 1.0 - ADB connector for Android control and automation
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SharpAdbClient;

namespace PhoneManagerApp.Core
{
    public class AdbConnector : IDeviceConnector
    {
        public string Name => "Android ADB Connector";
        public bool IsConnected { get; private set; }

        private DeviceData? _device;

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var client = new AdbClient();
                    var devices = client.GetDevices();

                    if (devices.Count == 0)
                    {
                        Debug.WriteLine("No ADB devices connected.");
                        return false;
                    }

                    _device = devices[0];
                    IsConnected = true;
                    Debug.WriteLine($"Connected to {_device.Name} via ADB.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ADB connection error: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<IEnumerable<string>> GetFilesAsync(string? path = null)
        {
            // For now, list installed packages instead of files (as an example)
            return await Task.Run(() =>
            {
                var results = new List<string>();

                try
                {
                    if (!IsConnected || _device == null)
                    {
                        results.Add("No device connected via ADB.");
                        return results;
                    }

                    var client = new AdbClient();
                    var receiver = new ConsoleOutputReceiver();
                    client.ExecuteRemoteCommand("pm list packages", _device, receiver);
                    results.AddRange(receiver.ToString().Split('\n'));
                }
                catch (Exception ex)
                {
                    results.Add($"Error executing command: {ex.Message}");
                }

                return results;
            });
        }

        // Example method for toggling notifications
        public async Task<bool> ToggleNotificationsAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_device == null)
                        return false;

                    var client = new AdbClient();
                    var command = enabled
                        ? "settings put global heads_up_notifications_enabled 1"
                        : "settings put global heads_up_notifications_enabled 0";

                    client.ExecuteRemoteCommand(command, _device, null);
                    Debug.WriteLine($"Notifications {(enabled ? "enabled" : "disabled")} successfully.");
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
}

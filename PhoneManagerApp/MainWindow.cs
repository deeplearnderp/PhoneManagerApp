#nullable disable
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;
using SharpAdbClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace PhoneManagerApp
{
    public class MainWindow : Form
    {
        private ToolbarPanel toolbar;
        private DeviceInfoPanel deviceInfoPanel;
        private TerminalPanel terminal;
        private StatusBarPanel statusBar;

        private AdbConnector adbConnector;
        private Process adbShellProcess;

        private System.Windows.Forms.Timer autoRefreshTimer;
        private System.Windows.Forms.Timer autoRefreshPulseTimer;

        private bool autoRefreshEnabled = true;
        private bool autoRefreshPulseVisible = true;
        private bool androidTerminalMode = true;

        private const int AutoRefreshInterval = 30000;

        public MainWindow()
        {
            Text = "Phone Manager App";
            Width = 1400;
            Height = 750;
            BackColor = Color.White;
            MinimumSize = new Size(900, 600);

            InitializeLayout();
            InitializeAdb();
            InitializeTimers();
        }

        // ================================
        // 🧩 Layout Setup
        // ================================
        private void InitializeLayout()
        {
            toolbar = new ToolbarPanel { Dock = DockStyle.Top, Height = 36 };
            toolbar.ConnectPhoneClicked += async (_, _) => await ConnectPhoneAsync();
            toolbar.ToggleNotificationsClicked += async (_, _) => await ToggleNotificationsAsync();
            toolbar.ToggleTerminalClicked += (_, _) => terminal.ToggleVisibility();
            toolbar.ClearOutputClicked += (_, _) => terminal.ClearOutput();

            deviceInfoPanel = new DeviceInfoPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            terminal = new TerminalPanel
            {
                Dock = DockStyle.Bottom,
                Height = 360
            };
            terminal.CommandEntered += async (_, cmd) => await ExecuteTerminalCommandAsync(cmd);

            statusBar = new StatusBarPanel
            {
                Dock = DockStyle.Bottom,
                Height = 32
            };
            statusBar.AutoRefreshClicked += (_, _) => ToggleAutoRefresh();
            statusBar.TerminalModeClicked += (_, _) => ToggleTerminalMode();

            Controls.Add(deviceInfoPanel);
            Controls.Add(terminal);
            Controls.Add(statusBar);
            Controls.Add(toolbar);
        }

        private void InitializeAdb()
        {
            adbConnector = new AdbConnector();
            terminal.AppendOutput("ADB Terminal initialized.");
        }

        private void InitializeTimers()
        {
            autoRefreshTimer = new System.Windows.Forms.Timer { Interval = AutoRefreshInterval };
            autoRefreshTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
            autoRefreshTimer.Start();

            autoRefreshPulseTimer = new System.Windows.Forms.Timer { Interval = 600 };
            autoRefreshPulseTimer.Tick += AutoRefreshPulse_Tick;
            autoRefreshPulseTimer.Start();
        }

        // ================================
        // 📡 ADB Connection
        // ================================
        private async Task ConnectPhoneAsync()
        {
            try
            {
                terminal.AppendOutput("[Connect] Connecting...");
                bool connected = await adbConnector.ConnectAsync();

                if (connected)
                {
                    string serial = adbConnector.GetConnectedDevice()?.Serial ?? "Unknown";
                    statusBar.SetConnectionStatus(true, serial);
                    terminal.AppendOutput("✅ Connected successfully.");
                    StartPersistentAdbShell();
                    await RefreshDeviceStatsAsync();
                }
                else
                {
                    statusBar.SetConnectionStatus(false);
                    terminal.AppendError("❌ Failed to connect.");
                }
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Connection error: {ex.Message}");
            }
        }

        private async Task ToggleNotificationsAsync()
        {
            if (adbConnector == null)
            {
                terminal.AppendError("⚠️ ADB connector not initialized.");
                return;
            }

            bool success = await adbConnector.ToggleNotificationsAsync(false);
            terminal.AppendOutput(success ? "🔕 Notifications turned OFF." : "❌ Failed to toggle notifications.");
        }

        // ================================
        // 🔁 Auto Refresh
        // ================================
        private void ToggleAutoRefresh()
        {
            autoRefreshEnabled = !autoRefreshEnabled;

            if (autoRefreshEnabled)
            {
                autoRefreshTimer.Start();
                autoRefreshPulseTimer.Start();
                terminal.AppendOutput("🔄 Auto Refresh enabled.");
            }
            else
            {
                autoRefreshTimer.Stop();
                autoRefreshPulseTimer.Stop();
                terminal.AppendOutput("⏸ Auto Refresh paused.");
            }

            statusBar.SetAutoRefreshStatus(autoRefreshEnabled);
        }

        private async Task RefreshDeviceStatsAsync()
        {
            if (!autoRefreshEnabled || adbConnector == null || !adbConnector.IsConnected)
                return;

            try
            {
                var client = new AdbClient();
                var device = adbConnector.GetConnectedDevice();
                if (device == null) return;

                // ========== Collect all data ==========
                string batteryRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys battery | grep level");
                string wifiRaw = await ExecuteAdbCommandAsync(client, device, "dumpsys wifi | grep RSSI");
                string storageRaw = await ExecuteAdbCommandAsync(client, device,
                    "df /data /storage/emulated/0 | grep -E '/data|/storage/emulated/0'");
                string diskStatsRaw = await ExecuteAdbCommandAsync(client, device,
                    "dumpsys diskstats"
                );
                deviceInfoPanel.UpdateStorageBreakdown(diskStatsRaw);

                // ========== Parse ==========
                string batteryLevel = ParseBatteryLevel(batteryRaw);
                string wifiSignal = ParseWifiSignal(wifiRaw);
                string storageUsage = ParseStorageUsage(storageRaw);

                // ========== Update UI ==========
                deviceInfoPanel.UpdateStorageBreakdown(diskStatsRaw);
                deviceInfoPanel.UpdateDeviceInfo(device.Model ?? "Unknown", device.Serial ?? "—",
                    batteryLevel, wifiSignal, storageUsage);

                UpdateDeviceDropdown(device.Model, storageUsage);
                statusBar.SetLastUpdated(DateTime.Now);
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Auto-refresh failed: {ex.Message}");
            }
        }

        private async Task<string> ExecuteAdbCommandAsync(AdbClient client, DeviceData device, string command)
        {
            var receiver = new ConsoleOutputReceiver();
            await Task.Run(() => client.ExecuteRemoteCommand(command, device, receiver));
            return receiver.ToString().Trim();
        }

        // ================================
        // 🧮 Parsing Helpers
        // ================================
        private string ParseBatteryLevel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            var match = Regex.Match(raw, @"level[:=]\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups[1].Value}%" : "—";
        }

        private string ParseWifiSignal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";

            // Try to find RSSI value
            var match = Regex.Match(raw, @"RSSI[:=]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "—";

            int rssi = int.Parse(match.Groups[1].Value);

            // Convert RSSI to signal strength description
            string strength;
            if (rssi >= -50)
                strength = "Excellent 📶📶📶📶";
            else if (rssi >= -60)
                strength = "Good 📶📶📶";
            else if (rssi >= -70)
                strength = "Fair 📶📶";
            else if (rssi >= -80)
                strength = "Weak 📶";
            else
                strength = "Very Weak ❌";

            return $"{strength} ({rssi} dBm)";
        }

        private string ParseStorageUsage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            var match = Regex.Match(raw, @"(\d+)%", RegexOptions.Multiline);
            return match.Success ? $"{match.Groups[1].Value}% used" : "—";
        }

        private void UpdateDeviceDropdown(string deviceName, string storageUsage)
        {
            toolbar.ComboDevices.Items.Clear();
            toolbar.ComboDevices.Items.Add($"{deviceName} — {storageUsage}");
            toolbar.ComboDevices.SelectedIndex = 0;
        }

        private void AutoRefreshPulse_Tick(object sender, EventArgs e)
        {
            autoRefreshPulseVisible = !autoRefreshPulseVisible;
            var color = autoRefreshPulseVisible ? Color.Lime : Color.Green;
            statusBar.Invoke(new Action(() => statusBar.ForeColor = color));
        }

        // ================================
        // 💻 Terminal Logic
        // ================================
        private void StartPersistentAdbShell()
        {
            try
            {
                adbShellProcess?.Kill();
                adbShellProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "adb",
                        Arguments = "shell",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                adbShellProcess.OutputDataReceived += (_, e) => { if (e.Data != null) terminal.AppendOutput(e.Data); };
                adbShellProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) terminal.AppendError(e.Data); };
                adbShellProcess.Start();
                adbShellProcess.BeginOutputReadLine();
                adbShellProcess.BeginErrorReadLine();
                terminal.AppendOutput("📡 Persistent Android shell started.");
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Failed to start ADB shell: {ex.Message}");
            }
        }

        private async Task ExecuteTerminalCommandAsync(string command)
        {
            try
            {
                if (androidTerminalMode)
                {
                    if (adbConnector?.IsConnected == true)
                    {
                        if (adbShellProcess == null || adbShellProcess.HasExited)
                            StartPersistentAdbShell();

                        await adbShellProcess.StandardInput.WriteLineAsync(command);
                    }
                    else
                    {
                        terminal.AppendError("⚠️ Not connected to Android device.");
                    }
                }
                else
                {
                    await ExecutePcCommandAsync(command);
                }
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Command error: {ex.Message}");
            }
        }

        private async Task ExecutePcCommandAsync(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c {command}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    terminal.AppendOutput(output);

                if (!string.IsNullOrEmpty(error))
                    terminal.AppendError(error);
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ PC command error: {ex.Message}");
            }
        }

        private void ToggleTerminalMode()
        {
            androidTerminalMode = !androidTerminalMode;
            string mode = androidTerminalMode ? "Android" : "PC";
            terminal.AppendOutput($"🖥 Switched to {mode} Terminal mode.");

            statusBar.SetTerminalMode(androidTerminalMode);

            if (!androidTerminalMode && adbShellProcess != null)
            {
                try { adbShellProcess.Kill(); } catch { }
                adbShellProcess = null;
            }
        }
    }
}

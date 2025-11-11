#nullable disable
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;
using SharpAdbClient;
using Timer = System.Windows.Forms.Timer;

namespace PhoneManagerApp
{
    /// <summary>
    /// Main application window that manages ADB connections and UI synchronization.
    /// </summary>
    public class MainWindow : Form
    {
        private ToolbarPanel toolbar;
        private DeviceInfoPanel deviceInfoPanel;
        private TerminalPanel terminal;
        private StatusBarPanel statusBar;

        private AdbConnector adbConnector;
        private Process adbShellProcess;

        private Timer autoRefreshTimer;
        private Timer autoRefreshPulseTimer;

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
            // Toolbar (top)
            toolbar = new ToolbarPanel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            toolbar.ConnectPhoneClicked += async (_, _) => await ConnectPhoneAsync();
            toolbar.ToggleNotificationsClicked += async (_, _) => await ToggleNotificationsAsync();
            toolbar.ToggleTerminalClicked += (_, _) => terminal.ToggleVisibility();
            toolbar.ClearOutputClicked += (_, _) => terminal.ClearOutput();

            // Device Info (center)
            deviceInfoPanel = new DeviceInfoPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Terminal (bottom)
            terminal = new TerminalPanel
            {
                Dock = DockStyle.Bottom,
                Height = 220
            };
            terminal.CommandEntered += async (_, cmd) => await ExecuteTerminalCommandAsync(cmd);

            // Status Bar (footer)
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
            // Auto-refresh timer
            autoRefreshTimer = new Timer { Interval = AutoRefreshInterval };
            autoRefreshTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
            autoRefreshTimer.Start();

            // Pulse animation timer
            autoRefreshPulseTimer = new Timer { Interval = 600 };
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
                var receiver = new ConsoleOutputReceiver();

                await Task.Run(() =>
                {
                    client.ExecuteRemoteCommand("dumpsys battery | grep level", device, receiver);
                    client.ExecuteRemoteCommand("dumpsys wifi | grep RSSI", device, receiver);
                    client.ExecuteRemoteCommand("df /data /sdcard | grep /data", device, receiver);
                });

                // Simulated stats (placeholder parsing)
                deviceInfoPanel.UpdateDeviceInfo(
                    device.Model ?? "Unknown",
                    device.Serial ?? "—",
                    "91%",
                    "—"
                );

                statusBar.SetLastUpdated(DateTime.Now);
                terminal.AppendOutput("[AutoRefresh] Updated device stats.");
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Auto-refresh failed: {ex.Message}");
            }
        }

        private void AutoRefreshPulse_Tick(object sender, EventArgs e)
        {
            // Pulse between Lime and Green for Auto-Refresh label text
            autoRefreshPulseVisible = !autoRefreshPulseVisible;
            var color = autoRefreshPulseVisible ? Color.Lime : Color.Green;
            statusBar.Invoke(new Action(() =>
            {
                statusBar.ForeColor = color;
            }));
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

#nullable disable
using System.Diagnostics;
using PhoneManagerApp.Core;
using PhoneManagerApp.UI.Panels;
using PhoneManagerApp.Core.Managers;
using Timer = System.Windows.Forms.Timer;

namespace PhoneManagerApp.UI;

public class MainWindow : Form
{
    private const int AutoRefreshInterval = 30000;

    private AdbConnector _adbConnector;
    private Process _adbShellProcess;
    private bool _androidTerminalMode = true;

    private bool _autoRefreshEnabled = true;
    private Timer _autoRefreshPulseTimer;
    private bool _autoRefreshPulseVisible = true;

    private Timer _autoRefreshTimer;
    private DeviceInfoPanel _deviceInfoPanel;
    private NotificationPanel _notificationPanel;
    private DeviceManager _deviceManager;
    private StatusBarPanel _statusBar;
    private TerminalPanel _terminal;
    private ToolbarPanel _toolbar;

    private SplitContainer _splitContainer;
    private TabControl _infoTabs;

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

        // 🧭 Ensure terminal height applies correctly after layout
        Load += (_, _) =>
        {
            try
            {
                int terminalHeight = 320;
                int totalUsable = ClientSize.Height - _statusBar.Height - _toolbar.Height;
                _splitContainer.SplitterDistance = Math.Max(200, totalUsable - terminalHeight);
            }
            catch { /* ignore if layout not ready */ }
        };
    }

    // ================================
    // 🧩 Layout Setup
    // ================================
    private void InitializeLayout()
    {
        // --- Toolbar ---
        _toolbar = new ToolbarPanel { Dock = DockStyle.Top, Height = 36 };
        _toolbar.ConnectPhoneClicked += async (_, _) => await ConnectPhoneAsync();
        _toolbar.ToggleNotificationsClicked += (_, _) => ToggleToNotificationsTab();
        _toolbar.ToggleTerminalClicked += (_, _) => _terminal.ToggleVisibility();
        _toolbar.ClearOutputClicked += (_, _) => _terminal.ClearOutput();

        // --- Device Info Panel ---
        _deviceInfoPanel = new DeviceInfoPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // --- Notification Panel ---
        _notificationPanel = new NotificationPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.WhiteSmoke
        };

        // --- TabControl for Device Info + Notifications ---
        _infoTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        // 🌟 ImageList for Tab Icons
        var imageList = new ImageList
        {
            ImageSize = new Size(20, 20),
            ColorDepth = ColorDepth.Depth32Bit
        };
        imageList.Images.Add("device", LoadEmbeddedImage("PhoneManagerApp.Resources.device.png"));
        imageList.Images.Add("notification", LoadEmbeddedImage("PhoneManagerApp.Resources.notification.png"));
        _infoTabs.ImageList = imageList;

        // 🎨 Custom draw to ensure icons appear properly
        _infoTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _infoTabs.Padding = new Point(22, 4);
        _infoTabs.DrawItem += (s, e) =>
        {
            var tab = _infoTabs.TabPages[e.Index];
            var img = _infoTabs.ImageList?.Images[tab.ImageKey];
            var bounds = e.Bounds;

            // Draw background
            e.Graphics.FillRectangle(SystemBrushes.Control, bounds);

            // Draw icon
            if (img != null)
                e.Graphics.DrawImage(img, bounds.Left + 4, bounds.Top + 2, 16, 16);

            // Draw text
            TextRenderer.DrawText(
                e.Graphics,
                tab.Text,
                _infoTabs.Font,
                new Point(bounds.Left + 26, bounds.Top + 3),
                SystemColors.ControlText);
        };

        // --- Tabs ---
        var deviceTab = new TabPage("Device Info")
        {
            ImageKey = "device",
            BackColor = Color.White
        };
        var notifTab = new TabPage("Notifications")
        {
            ImageKey = "notification",
            BackColor = Color.WhiteSmoke
        };

        deviceTab.Controls.Add(_deviceInfoPanel);
        notifTab.Controls.Add(_notificationPanel);

        _infoTabs.TabPages.Add(deviceTab);
        _infoTabs.TabPages.Add(notifTab);

        // --- Terminal Panel ---
        _terminal = new TerminalPanel
        {
            Dock = DockStyle.Fill,
            Height = 250
        };
        _terminal.CommandEntered += async (_, cmd) => await ExecuteTerminalCommandAsync(cmd);

        // --- Status Bar ---
        _statusBar = new StatusBarPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32
        };
        _statusBar.AutoRefreshClicked += (_, _) => ToggleAutoRefresh();
        _statusBar.TerminalModeClicked += (_, _) => ToggleTerminalMode();

        // --- SplitContainer for Tabs + Terminal ---
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400,
            SplitterWidth = 6,
            IsSplitterFixed = false,
            BackColor = Color.FromArgb(230, 230, 230),
            Panel1MinSize = 200,
            Panel2MinSize = 120
        };

        _splitContainer.Panel1.Controls.Add(_infoTabs);
        _splitContainer.Panel2.Controls.Add(_terminal);

        // --- Add to main window ---
        Controls.Add(_splitContainer);
        Controls.Add(_statusBar);
        Controls.Add(_toolbar);
    }

    // ================================
    // 🖼️ Resource Loader
    // ================================
    private Image LoadEmbeddedImage(string resourcePath)
    {
        var assembly = typeof(MainWindow).Assembly;
        using (var stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null)
            {
                _terminal?.AppendError($"⚠️ Resource not found: {resourcePath}");
                return null;
            }
            return Image.FromStream(stream);
        }
    }

    // ================================
    // 🔔 Tab Toggle
    // ================================
    private void ToggleToNotificationsTab()
    {
        if (_infoTabs == null) return;
        _infoTabs.SelectedIndex = (_infoTabs.SelectedIndex == 1) ? 0 : 1;
    }

    // ================================
    // ⚙️ ADB Initialization
    // ================================
    private void InitializeAdb()
    {
        _adbConnector = new AdbConnector();
        _deviceManager = new DeviceManager(_adbConnector);
        _terminal.AppendOutput("ADB Terminal initialized.");
    }

    private void InitializeTimers()
    {
        _autoRefreshTimer = new Timer { Interval = AutoRefreshInterval };
        _autoRefreshTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
        _autoRefreshTimer.Start();

        _autoRefreshPulseTimer = new Timer { Interval = 600 };
        _autoRefreshPulseTimer.Tick += AutoRefreshPulse_Tick;
        _autoRefreshPulseTimer.Start();
    }

    // ================================
    // 📡 ADB Connection
    // ================================
    private async Task ConnectPhoneAsync()
    {
        try
        {
            _terminal.AppendOutput("[Connect] Connecting...");
            var connected = await _adbConnector.ConnectAsync();

            if (connected)
            {
                var serial = _adbConnector.GetConnectedDevice()?.Serial ?? "Unknown";
                _statusBar.SetConnectionStatus(true, serial);
                _terminal.AppendOutput("✅ Connected successfully.");
                StartPersistentAdbShell();
                await RefreshDeviceStatsAsync();
                await _notificationPanel.RefreshNotificationsAsync(_adbConnector, _terminal);
            }
            else
            {
                _statusBar.SetConnectionStatus(false);
                _terminal.AppendError("❌ Failed to connect.");
            }
        }
        catch (Exception ex)
        {
            _terminal.AppendError($"⚠️ Connection error: {ex.Message}");
        }
    }

    // ================================
    // 🔁 Auto Refresh
    // ================================
    private void ToggleAutoRefresh()
    {
        _autoRefreshEnabled = !_autoRefreshEnabled;

        if (_autoRefreshEnabled)
        {
            _autoRefreshTimer.Start();
            _autoRefreshPulseTimer.Start();
            _terminal.AppendOutput("🔄 Auto Refresh enabled.");
        }
        else
        {
            _autoRefreshTimer.Stop();
            _autoRefreshPulseTimer.Stop();
            _terminal.AppendOutput("⏸ Auto Refresh paused.");
        }

        _statusBar.SetAutoRefreshStatus(_autoRefreshEnabled);
    }

    private async Task RefreshDeviceStatsAsync()
    {
        if (!_autoRefreshEnabled || _adbConnector == null || !_adbConnector.IsConnected)
            return;

        try
        {
            var info = await _deviceManager.GetDeviceStatsAsync();
            if (info == null)
            {
                _terminal.AppendError("⚠️ Failed to get device stats.");
                return;
            }

            _deviceInfoPanel.UpdateStorageBreakdown(info.DiskStatsRaw);
            _deviceInfoPanel.UpdateDeviceInfo(
                info.DeviceName,
                info.IpAddress,
                info.BatteryLevel,
                info.WifiSignal,
                info.StorageUsage,
                info.ExtraIp,
                info.IsCharging
            );

            UpdateDeviceDropdown(info.DeviceName, info.StorageUsage);
            _statusBar.SetLastUpdated(info.LastUpdated);

            await _notificationPanel.RefreshNotificationsAsync(_adbConnector, _terminal);
        }
        catch (Exception ex)
        {
            _terminal.AppendError($"⚠️ Auto-refresh failed: {ex.Message}");
        }
    }

    private void UpdateDeviceDropdown(string deviceName, string storageUsage)
    {
        _toolbar.ComboDevices.Items.Clear();
        _toolbar.ComboDevices.Items.Add($"{deviceName} — {storageUsage}");
        _toolbar.ComboDevices.SelectedIndex = 0;
    }

    private void AutoRefreshPulse_Tick(object sender, EventArgs e)
    {
        _autoRefreshPulseVisible = !_autoRefreshPulseVisible;
        var color = _autoRefreshPulseVisible ? Color.Lime : Color.Green;
        _statusBar.Invoke(new Action(() => _statusBar.ForeColor = color));
    }

    // ================================
    // 💻 Terminal Logic
    // ================================
    private void StartPersistentAdbShell()
    {
        try
        {
            _adbShellProcess?.Kill();
            _adbShellProcess = new Process
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
            _adbShellProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) _terminal.AppendOutput(e.Data);
            };
            _adbShellProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) _terminal.AppendError(e.Data);
            };
            _adbShellProcess.Start();
            _adbShellProcess.BeginOutputReadLine();
            _adbShellProcess.BeginErrorReadLine();
            _terminal.AppendOutput("📡 Persistent Android shell started.");
        }
        catch (Exception ex)
        {
            _terminal.AppendError($"⚠️ Failed to start ADB shell: {ex.Message}");
        }
    }

    private async Task ExecuteTerminalCommandAsync(string command)
    {
        try
        {
            if (_androidTerminalMode)
            {
                if (_adbConnector?.IsConnected == true)
                {
                    if (_adbShellProcess == null || _adbShellProcess.HasExited)
                        StartPersistentAdbShell();

                    await _adbShellProcess.StandardInput.WriteLineAsync(command);
                }
                else
                {
                    _terminal.AppendError("⚠️ Not connected to Android device.");
                }
            }
            else
            {
                await ExecutePcCommandAsync(command);
            }
        }
        catch (Exception ex)
        {
            _terminal.AppendError($"⚠️ Command error: {ex.Message}");
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
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(output))
                _terminal.AppendOutput(output);

            if (!string.IsNullOrEmpty(error))
                _terminal.AppendError(error);
        }
        catch (Exception ex)
        {
            _terminal.AppendError($"⚠️ PC command error: {ex.Message}");
        }
    }

    private void ToggleTerminalMode()
    {
        _androidTerminalMode = !_androidTerminalMode;
        var mode = _androidTerminalMode ? "Android" : "PC";
        _terminal.AppendOutput($"🖥 Switched to {mode} Terminal mode.");

        _statusBar.SetTerminalMode(_androidTerminalMode);

        if (!_androidTerminalMode && _adbShellProcess != null)
        {
            try { _adbShellProcess.Kill(); } catch { }
            _adbShellProcess = null;
        }
    }
}

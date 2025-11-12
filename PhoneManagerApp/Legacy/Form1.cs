using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;
using SharpAdbClient;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        // ==============================
        // ⚙️ UI CONSTANTS
        // ==============================
        private const int TerminalHeight = 200;
        private const int ToolbarHeight = 34;
        private const int AutoRefreshInterval = 30000;

        private readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private readonly Color TerminalTextColor = Color.Lime;
        private readonly Color InputBackground = Color.FromArgb(35, 35, 35);
        private readonly Color InputTextColor = Color.White;
        private readonly Color AutoRefreshOnColor = Color.FromArgb(0, 180, 0);
        private readonly Color AutoRefreshOffColor = Color.FromArgb(200, 30, 30);

        // ==============================
        // 🧩 CORE COMPONENTS
        // ==============================
        private AdbConnector adbConnector;
        private RichTextBox rtbTerminal;
        private TextBox txtTerminalInput;
        private ComboBox comboConnectionMode;
        private ComboBox comboAdbSource;
        private ComboBox comboDevices;
        private Button btnConnectPhone;
        private Button btnToggleNotifications;
        private Button btnShowTerminal;
        private Button btnAutoRefresh;
        private Button btnTerminalMode;
        private Button btnClearOutput;
        private Label lblStatus;

        private Label lblDeviceName, lblDeviceIp, lblBattery, lblWifi, lblStorage, lblLastUpdate;
        private DeviceInfoDisplay deviceDisplay;

        // ==============================
        // ⏱️ TIMERS
        // ==============================
        private System.Windows.Forms.Timer autoRefreshTimer;
        private System.Windows.Forms.Timer expandTimer;
        private ToolTip hoverTooltip;
        private bool autoRefreshEnabled = true;

        // ==============================
        // 💻 TERMINAL STATE
        // ==============================
        private bool androidTerminalMode = true;
        private Process adbShellProcess;

        // ==============================
        // 📦 STORAGE EXPANDABLE
        // ==============================
        private bool storageExpanded = false;
        private Label storageDetailsLabel;
        private int targetHeight = 0;

        // ==============================
        // 🏗️ CONSTRUCTOR
        // ==============================
        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            InitializeAutoRefresh();
        }

        // ==============================
        // 🧱 UI SETUP
        // ==============================
        private void InitializeUI()
        {
            Text = "Phone Manager App";
            Width = 1400;
            Height = 650;
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 600);

            // ====== MAIN LAYOUT ======
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ToolbarHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TerminalHeight + 28));
            Controls.Add(layout);

            // ====== TOP TOOLBAR ======
            var panelTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true,
                WrapContents = false
            };
            layout.Controls.Add(panelTop, 0, 0);

            comboConnectionMode = new ComboBox { Width = 140 };
            comboConnectionMode.Items.Add("ADB Control Mode");
            comboConnectionMode.SelectedIndex = 0;
            panelTop.Controls.Add(comboConnectionMode);

            comboAdbSource = new ComboBox { Width = 140 };
            comboAdbSource.Items.AddRange(new[] { "Wi-Fi (Secure)", "USB" });
            comboAdbSource.SelectedIndex = 0;
            panelTop.Controls.Add(comboAdbSource);

            comboDevices = new ComboBox { Width = 180 };
            comboDevices.Items.Add("System ADB");
            comboDevices.SelectedIndex = 0;
            panelTop.Controls.Add(comboDevices);

            btnConnectPhone = new Button { Text = "Connect Phone", Width = 110 };
            btnConnectPhone.Click += async (_, _) => await ConnectPhoneAsync();
            panelTop.Controls.Add(btnConnectPhone);

            btnToggleNotifications = new Button { Text = "Toggle Notifications", Width = 140 };
            btnToggleNotifications.Click += async (_, _) => await ToggleNotificationsAsync();
            panelTop.Controls.Add(btnToggleNotifications);

            btnShowTerminal = new Button { Text = "Hide Terminal", Width = 110 };
            btnShowTerminal.Click += (_, _) => ToggleTerminalVisibility();
            panelTop.Controls.Add(btnShowTerminal);

            btnClearOutput = new Button { Text = "🖋 Clear Output", Width = 120 };
            btnClearOutput.Click += (_, _) => ClearTerminalOutput();
            panelTop.Controls.Add(btnClearOutput);

            btnAutoRefresh = new Button
            {
                Text = "AR",
                Width = 45,
                BackColor = AutoRefreshOnColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAutoRefresh.FlatAppearance.BorderSize = 0;
            btnAutoRefresh.Click += BtnAutoRefresh_Click;
            panelTop.Controls.Add(btnAutoRefresh);

            btnTerminalMode = new Button
            {
                Text = "Android",
                Width = 80,
                BackColor = Color.LimeGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnTerminalMode.FlatAppearance.BorderSize = 0;
            btnTerminalMode.Click += (_, _) => ToggleTerminalMode();
            panelTop.Controls.Add(btnTerminalMode);

            lblStatus = new Label
            {
                Text = "Status: 🔴 Disconnected",
                ForeColor = Color.Red,
                AutoSize = true,
                Padding = new Padding(20, 6, 0, 0)
            };
            panelTop.Controls.Add(lblStatus);

            // ====== DEVICE INFO SECTION ======
            var pnlDeviceInfo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(20)
            };
            layout.Controls.Add(pnlDeviceInfo, 0, 1);

            lblDeviceName = CreateInfoLabel("Device:");
            lblDeviceIp = CreateInfoLabel("IP Address:");
            lblBattery = CreateInfoLabel("Battery:");
            lblWifi = CreateInfoLabel("Wi-Fi Signal:");
            lblStorage = CreateInfoLabel("Storage:");
            lblLastUpdate = CreateInfoLabel("Last Update:");

            lblStorage.Cursor = Cursors.Hand;
            lblStorage.Click += ToggleStorageExpanded;

            storageDetailsLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Consolas", 9, FontStyle.Regular),
                ForeColor = Color.Black,
                Height = 0,
                Width = 300,
                Visible = false,
                Margin = new Padding(25, 0, 0, 0)
            };

            pnlDeviceInfo.Controls.AddRange(new Control[]
            {
                lblDeviceName,
                lblDeviceIp,
                lblBattery,
                lblWifi,
                lblStorage,
                storageDetailsLabel,
                lblLastUpdate
            });

            deviceDisplay = new DeviceInfoDisplay(
                lblDeviceName, lblDeviceIp, lblBattery, lblWifi, lblStorage, lblLastUpdate
            );

            expandTimer = new System.Windows.Forms.Timer { Interval = 15 };
            expandTimer.Tick += ExpandTimer_Tick;

            // ====== TERMINAL SECTION ======
            var terminalContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TerminalBackground
            };
            layout.Controls.Add(terminalContainer, 0, 2);

            rtbTerminal = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                BackColor = TerminalBackground,
                ForeColor = TerminalTextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            terminalContainer.Controls.Add(rtbTerminal);

            txtTerminalInput = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = InputBackground,
                ForeColor = InputTextColor,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtTerminalInput.KeyDown += TerminalInput_KeyDown;
            terminalContainer.Controls.Add(txtTerminalInput);

            WriteTerminal("ADB Terminal initialized.");
        }

        private Label CreateInfoLabel(string title)
        {
            return new Label
            {
                Text = $"{title} —",
                AutoSize = true,
                Width = 700,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.Black,
                Margin = new Padding(0, 2, 0, 2)
            };
        }

        // ==============================
        // 🔽 EXPANDABLE STORAGE
        // ==============================
        private void ToggleStorageExpanded(object sender, EventArgs e)
        {
            storageExpanded = !storageExpanded;
            storageDetailsLabel.Visible = true;

            string text = lblStorage.Text.Replace("▸", "").Replace("▾", "").Trim();
            lblStorage.Text = $"{text} {(storageExpanded ? "▾" : "▸")}";

            targetHeight = storageExpanded ? 100 : 0;
            expandTimer.Start();
        }

        private void ExpandTimer_Tick(object sender, EventArgs e)
        {
            int step = 10;
            if (storageExpanded)
            {
                if (storageDetailsLabel.Height < targetHeight)
                    storageDetailsLabel.Height += step;
                else expandTimer.Stop();
            }
            else
            {
                if (storageDetailsLabel.Height > targetHeight)
                    storageDetailsLabel.Height -= step;
                else
                {
                    storageDetailsLabel.Visible = false;
                    expandTimer.Stop();
                }
            }
        }

        private void UpdateStorageInfo(string adbOutput)
        {
            try
            {
                var dataMatch = Regex.Match(adbOutput, @"(\d+)%");
                double usedPercent = dataMatch.Success ? double.Parse(dataMatch.Groups[1].Value) : 0;
                double photosGB = 1.23, videosGB = 2.45, appsGB = 5.67, systemGB = 4.20, otherGB = 3.10, freeGB = 10.5;

                lblStorage.Text = $"Storage: Internal: {usedPercent:F0}% {(storageExpanded ? "▾" : "▸")}";
                storageDetailsLabel.Text =
                    $"Photos:  {photosGB:F2} GB\n" +
                    $"Videos:  {videosGB:F2} GB\n" +
                    $"Apps:    {appsGB:F2} GB\n" +
                    $"System:  {systemGB:F2} GB\n" +
                    $"Other:   {otherGB:F2} GB\n" +
                    $"Free:    {freeGB:F2} GB";
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ Failed to update storage info: {ex.Message}");
            }
        }

        // ==============================
        // 💻 TERMINAL HANDLERS
        // ==============================
        private async void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string command = txtTerminalInput.Text.Trim();
            txtTerminalInput.Clear();

            if (string.IsNullOrWhiteSpace(command)) return;
            WriteTerminal($"> {command}");
            if (command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                rtbTerminal.Clear();
                return;
            }
            await ExecuteTerminalCommandAsync(command);
        }

        private async Task ExecuteTerminalCommandAsync(string command)
        {
            if (androidTerminalMode)
            {
                if (adbConnector?.IsConnected == true)
                {
                    if (adbShellProcess == null || adbShellProcess.HasExited)
                        StartPersistentAdbShell();

                    await SendToAdbShellAsync(command);
                }
                else WriteTerminal("⚠️ Not connected to Android device.");
            }
            else await ExecutePcCommandAsync(command);
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
                if (!string.IsNullOrEmpty(output)) WriteTerminal(output);
                if (!string.IsNullOrEmpty(error)) WriteTerminal(error);
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ PC command error: {ex.Message}");
            }
        }

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
                adbShellProcess.OutputDataReceived += (_, e) => { if (e.Data != null) WriteTerminal(e.Data); };
                adbShellProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) WriteTerminal($"⚠️ {e.Data}"); };
                adbShellProcess.Start();
                adbShellProcess.BeginOutputReadLine();
                adbShellProcess.BeginErrorReadLine();
                WriteTerminal("📡 Persistent Android shell started.");
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ Failed to start ADB shell: {ex.Message}");
            }
        }

        private async Task SendToAdbShellAsync(string command)
        {
            try
            {
                if (adbShellProcess?.HasExited == false)
                    await adbShellProcess.StandardInput.WriteLineAsync(command);
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ Send error: {ex.Message}");
            }
        }

        private void ToggleTerminalMode()
        {
            androidTerminalMode = !androidTerminalMode;
            btnTerminalMode.Text = androidTerminalMode ? "Android" : "PC";
            btnTerminalMode.BackColor = androidTerminalMode ? Color.LimeGreen : Color.RoyalBlue;
            WriteTerminal(androidTerminalMode ? "Switched to Android Terminal mode." : "Switched to PC Terminal mode.");

            if (!androidTerminalMode && adbShellProcess != null)
            {
                try { adbShellProcess.Kill(); } catch { }
                adbShellProcess = null;
            }
        }

        private void ToggleTerminalVisibility()
        {
            if (rtbTerminal == null || txtTerminalInput == null)
            {
                MessageBox.Show("Terminal is not initialized yet.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool visible = !rtbTerminal.Visible;
            rtbTerminal.Visible = visible;
            txtTerminalInput.Visible = visible;
            btnShowTerminal.Text = visible ? "Hide Terminal" : "Show Terminal";
        }

        private void ClearTerminalOutput()
        {
            if (rtbTerminal == null) return;
            rtbTerminal.Clear();
            WriteTerminal("🧹 Output cleared.");
        }

        private void WriteTerminal(string message)
        {
            if (rtbTerminal == null) return;

            if (InvokeRequired)
            {
                Invoke(new Action(() => WriteTerminal(message)));
                return;
            }

            rtbTerminal.AppendText($"{message}\n");
            rtbTerminal.ScrollToCaret();
        }

        // ==============================
        // 📱 DEVICE CONNECTION
        // ==============================
        private async Task ConnectPhoneAsync()
        {
            try
            {
                WriteTerminal("[Connect] Connecting...");
                adbConnector ??= new AdbConnector();

                bool connected = await adbConnector.ConnectAsync();
                if (connected)
                {
                    lblStatus.ForeColor = Color.Green;
                    lblStatus.Text = $"Status: Connected ({adbConnector.GetConnectedDevice()?.Serial})";
                    WriteTerminal("✅ Connected successfully.");
                    StartPersistentAdbShell();
                    await RefreshDeviceStatsAsync();
                }
                else
                {
                    lblStatus.ForeColor = Color.Red;
                    lblStatus.Text = "Status: Disconnected";
                    WriteTerminal("❌ Failed to connect.");
                }
            }
            catch (Exception ex)
            {
                WriteTerminal($"❌ Connection error: {ex.Message}");
            }
        }

        private async Task ToggleNotificationsAsync()
        {
            if (adbConnector == null)
            {
                WriteTerminal("⚠️ ADB connector not initialized.");
                return;
            }

            bool success = await adbConnector.ToggleNotificationsAsync(enabled: false);
            WriteTerminal(success ? "🔕 Notifications turned OFF." : "❌ Failed to toggle notifications.");
        }

        // ==============================
        // 🔁 AUTO REFRESH
        // ==============================
        private void InitializeAutoRefresh()
        {
            autoRefreshTimer = new System.Windows.Forms.Timer();
            autoRefreshTimer.Interval = AutoRefreshInterval;
            autoRefreshTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
            autoRefreshTimer.Start();

            hoverTooltip = new ToolTip();
            hoverTooltip.SetToolTip(btnAutoRefresh, "Auto Refresh (ON)");
        }

        private void BtnAutoRefresh_Click(object sender, EventArgs e)
        {
            autoRefreshEnabled = !autoRefreshEnabled;
            if (autoRefreshEnabled)
            {
                autoRefreshTimer.Start();
                btnAutoRefresh.BackColor = AutoRefreshOnColor;
                hoverTooltip.SetToolTip(btnAutoRefresh, "Auto Refresh (ON)");
            }
            else
            {
                autoRefreshTimer.Stop();
                btnAutoRefresh.BackColor = AutoRefreshOffColor;
                hoverTooltip.SetToolTip(btnAutoRefresh, "Auto Refresh (OFF)");
            }
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
                    client.ExecuteRemoteCommand("df /data /sdcard /storage/emulated/0 | grep -E '/data|/sdcard|/storage/emulated/0'", device, receiver);
                    client.ExecuteRemoteCommand("dumpsys wifi | grep RSSI", device, receiver);
                });

                string output = receiver.ToString();
                UpdateOutput(output);
                UpdateStorageInfo(output);
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ Auto-refresh failed: {ex.Message}");
            }
        }

        // ==============================
        // 📊 DEVICE INFO DISPLAY
        // ==============================
        private void UpdateOutput(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateOutput(text)));
                return;
            }

            string model = adbConnector?.GetConnectedDevice()?.Model ?? "Unknown";
            string ip = adbConnector?.GetConnectedDevice()?.Serial ?? "—";
            deviceDisplay.Update(text, model, ip);
        }
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;
using SharpAdbClient;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        // ==============================
        // 🔧 UI Settings and Constants
        // ==============================
        private const int TerminalHeight = 200;
        private const int ToolbarHeight = 34;
        private const int AutoRefreshInterval = 30000; // 30 seconds
        private readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private readonly Color TerminalTextColor = Color.Lime;
        private readonly Color AutoRefreshOnColor = Color.FromArgb(0, 180, 0);
        private readonly Color AutoRefreshOffColor = Color.FromArgb(200, 30, 30);

        // ==============================
        // 🧩 Core Components
        // ==============================
        private AdbConnector adbConnector;
        private RichTextBox rtbOutput;
        private RichTextBox rtbTerminal;
        private TextBox txtTerminalInput;
        private ComboBox comboConnectionMode;
        private ComboBox comboAdbSource;
        private ComboBox comboDevices;
        private Button btnConnectPhone;
        private Button btnScanDevices;
        private Button btnToggleNotifications;
        private Button btnShowTerminal;
        private Button btnAutoRefresh;
        private Label lblStatus;

        // ==============================
        // ⚙️ Auto Refresh
        // ==============================
        private bool autoRefreshEnabled = true;
        private System.Windows.Forms.Timer autoRefreshTimer;
        private ToolTip hoverTooltip;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            InitializeTerminal();
            InitializeAutoRefresh();

            // start visible
            rtbTerminal.Visible = true;
            txtTerminalInput.Visible = true;
        }

        // ==============================
        // 🧱 Initialize UI
        // ==============================
        private void InitializeUI()
        {
            Text = "Phone Manager App";
            Width = 1400;
            Height = 650;
            BackColor = Color.White;

            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = ToolbarHeight,
                Padding = new Padding(10, 4, 10, 4)
            };
            Controls.Add(panelTop);

            // connection mode
            comboConnectionMode = new ComboBox { Width = 140 };
            comboConnectionMode.Items.AddRange(new[] { "ADB Control Mode" });
            comboConnectionMode.SelectedIndex = 0;
            panelTop.Controls.Add(comboConnectionMode);

            // source (Wi-Fi / USB)
            comboAdbSource = new ComboBox { Left = 150, Width = 140 };
            comboAdbSource.Items.AddRange(new[] { "Wi-Fi (Secure)", "USB" });
            comboAdbSource.SelectedIndex = 0;
            comboAdbSource.SelectedIndexChanged += ComboAdbSource_SelectedIndexChanged;
            panelTop.Controls.Add(comboAdbSource);

            // device list
            comboDevices = new ComboBox { Left = 300, Width = 180 };
            comboDevices.Items.AddRange(new[] { "System ADB", "User ADB" });
            comboDevices.SelectedIndex = 0;
            panelTop.Controls.Add(comboDevices);

            // connect
            btnConnectPhone = new Button { Text = "Connect Phone", Left = 490, Width = 110 };
            btnConnectPhone.Click += async (_, _) => await ConnectPhoneAsync();
            panelTop.Controls.Add(btnConnectPhone);

            // scan
            btnScanDevices = new Button { Text = "Scan Devices", Left = 610, Width = 120 };
            btnScanDevices.Click += async (_, _) => await ScanDevicesAsync();
            panelTop.Controls.Add(btnScanDevices);

            // notifications
            btnToggleNotifications = new Button { Text = "Toggle Notifications", Left = 740, Width = 150 };
            btnToggleNotifications.Click += async (_, _) => await ToggleNotificationsAsync();
            panelTop.Controls.Add(btnToggleNotifications);

            // terminal
            btnShowTerminal = new Button { Text = "Hide Terminal", Left = 900, Width = 110 };
            btnShowTerminal.Click += (_, _) => ToggleTerminalVisibility();
            panelTop.Controls.Add(btnShowTerminal);

            // auto-refresh
            btnAutoRefresh = new Button
            {
                Text = "AR",
                Left = 1020,
                Width = 45,
                BackColor = AutoRefreshOnColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAutoRefresh.FlatAppearance.BorderSize = 0;
            btnAutoRefresh.Click += BtnAutoRefresh_Click;
            panelTop.Controls.Add(btnAutoRefresh);

            // status
            lblStatus = new Label
            {
                Text = "Status: 🔴 Disconnected",
                ForeColor = Color.Red,
                Dock = DockStyle.Right,
                AutoSize = true
            };
            panelTop.Controls.Add(lblStatus);

            // output area
            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Consolas", 10f)
            };
            Controls.Add(rtbOutput);
        }

        // ==============================
        // 🧱 Terminal
        // ==============================
        private void InitializeTerminal()
        {
            rtbTerminal = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = TerminalHeight - 28,
                ReadOnly = true,
                Multiline = true,
                BackColor = TerminalBackground,
                ForeColor = TerminalTextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(rtbTerminal);

            txtTerminalInput = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f),
                BackColor = Color.Black,
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 0, 2)
            };
            txtTerminalInput.KeyDown += TxtTerminalInput_KeyDown;
            Controls.Add(txtTerminalInput);

            WriteTerminal("ADB Terminal initialized.");
        }

        // ==============================
        // 🕒 Auto Refresh
        // ==============================
        private void InitializeAutoRefresh()
        {
            autoRefreshTimer = new System.Windows.Forms.Timer();
            autoRefreshTimer.Interval = AutoRefreshInterval;
            autoRefreshTimer.Tick += async (_, _) => await RefreshDeviceStatsAsync();
            autoRefreshTimer.Start();

            hoverTooltip = new ToolTip();
            hoverTooltip.SetToolTip(btnAutoRefresh, "Auto Refresh (ON)");

            btnAutoRefresh.MouseEnter += (s, e) =>
            {
                hoverTooltip.Show("Auto Refresh", btnAutoRefresh, 0, -25);
                Task.Delay(10000).ContinueWith(_ =>
                {
                    if (btnAutoRefresh.IsHandleCreated)
                        btnAutoRefresh.BeginInvoke(new Action(() => hoverTooltip.Hide(btnAutoRefresh)));
                });
            };
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

        // ==============================
        // 💬 Terminal Helpers
        // ==============================
        private void WriteTerminal(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => WriteTerminal(message)));
                return;
            }
            rtbTerminal.AppendText($"> {message}\n");
            rtbTerminal.ScrollToCaret();
        }

        private async void TxtTerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            string command = txtTerminalInput.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;
            txtTerminalInput.Clear();
            WriteTerminal($"> {command}");

            if (adbConnector == null || !adbConnector.IsConnected)
            {
                WriteTerminal("⚠️ Not connected to a device.");
                return;
            }

            try
            {
                var client = new AdbClient();
                var device = adbConnector.GetConnectedDevice();
                var receiver = new ConsoleOutputReceiver();

                await Task.Run(() => client.ExecuteRemoteCommand(command, device, receiver));
                WriteTerminal(receiver.ToString().Trim());
            }
            catch (Exception ex)
            {
                WriteTerminal($"❌ Command failed: {ex.Message}");
            }
        }

        // ==============================
        // 📱 Connect Phone
        // ==============================
        private async Task ConnectPhoneAsync()
        {
            try
            {
                WriteTerminal("[Connect] Connecting...");

                if (adbConnector == null)
                    adbConnector = new AdbConnector();

                bool connected = await adbConnector.ConnectAsync();

                if (connected)
                {
                    lblStatus.ForeColor = Color.Green;
                    lblStatus.Text = $"Status: Connected ({adbConnector.GetConnectedDevice()?.Serial})";
                    WriteTerminal("✅ Connected successfully.");
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

        // ==============================
        // 🔍 Scan Devices
        // ==============================
        private async Task ScanDevicesAsync()
        {
            try
            {
                WriteTerminal("[Scan] Looking for devices...");
                var client = new AdbClient();
                var devices = await Task.Run(() => client.GetDevices());

                comboDevices.Items.Clear();
                foreach (var device in devices)
                    comboDevices.Items.Add($"{device.Serial} ({device.State})");

                if (comboDevices.Items.Count == 0)
                {
                    comboDevices.Items.Add("No devices found");
                    comboDevices.SelectedIndex = 0;
                    WriteTerminal("⚠️ No devices detected.");
                }
                else
                {
                    comboDevices.SelectedIndex = 0;
                    WriteTerminal($"✅ Found {comboDevices.Items.Count} device(s).");
                }
            }
            catch (Exception ex)
            {
                WriteTerminal($"❌ Scan failed: {ex.Message}");
            }
        }

        // ==============================
        // 🔔 Notifications
        // ==============================
        private async Task ToggleNotificationsAsync()
        {
            if (adbConnector == null)
            {
                WriteTerminal("⚠️ ADB connector not initialized.");
                return;
            }

            bool success = await adbConnector.ToggleNotificationsAsync(enabled: false);
            WriteTerminal(success
                ? "🔕 Notifications turned OFF."
                : "❌ Failed to toggle notifications.");
        }

        // ==============================
        // 📊 Refresh Device Stats
        // ==============================
        private async Task RefreshDeviceStatsAsync()
        {
            if (!autoRefreshEnabled) return;

            try
            {
                if (adbConnector == null || !adbConnector.IsConnected)
                {
                    WriteTerminal("⚠️ Cannot refresh — not connected.");
                    return;
                }

                string batteryCmd = "dumpsys battery | grep level";
                string storageCmd = "df /data | tail -1";
                string wifiCmd = "dumpsys wifi | grep 'RSSI'";
                string cellCmd = "dumpsys telephony.registry | grep 'mSignalStrength'";

                var client = new AdbClient();
                var device = adbConnector.GetConnectedDevice();
                var receiver = new ConsoleOutputReceiver();

                await Task.Run(() =>
                {
                    client.ExecuteRemoteCommand(batteryCmd, device, receiver);
                    client.ExecuteRemoteCommand(storageCmd, device, receiver);
                    client.ExecuteRemoteCommand(wifiCmd, device, receiver);
                    client.ExecuteRemoteCommand(cellCmd, device, receiver);
                });

                string output = receiver.ToString();
                string batteryLevel = ExtractLine(output, "level");
                string storage = ExtractLine(output, "/data");
                string wifi = ExtractLine(output, "RSSI");
                string cell = ExtractLine(output, "mSignalStrength");

                string formatted =
                    $"📱 Device Stats ({DateTime.Now:T})\n" +
                    $"• Battery Level: {batteryLevel}\n" +
                    $"• Storage Usage: {storage}\n" +
                    $"• Wi-Fi Strength: {wifi}\n" +
                    $"• Cellular Signal: {cell}\n";

                UpdateOutput(formatted);
            }
            catch (Exception ex)
            {
                WriteTerminal($"⚠️ Auto-refresh failed: {ex.Message}");
            }
        }

        // ==============================
        // Helpers
        // ==============================
        private string ExtractLine(string text, string keyword)
        {
            foreach (var line in text.Split('\n'))
                if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return line.Trim();
            return "N/A";
        }

        private void UpdateOutput(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateOutput(text)));
                return;
            }

            rtbOutput.Clear();
            rtbOutput.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
            rtbOutput.AppendText(text);
        }

        private void ToggleTerminalVisibility()
        {
            bool visible = !rtbTerminal.Visible;
            rtbTerminal.Visible = visible;
            txtTerminalInput.Visible = visible;
            btnShowTerminal.Text = visible ? "Hide Terminal" : "Show Terminal";
        }

        // ==============================
        // 🔄 Source Change (Wi-Fi / USB)
        // ==============================
        private void ComboAdbSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = comboAdbSource.SelectedItem.ToString();
            if (selected == "USB")
                WriteTerminal("🔌 Mode changed: USB selected.");
            else
                WriteTerminal("📶 Mode changed: Wi-Fi (Secure) selected.");
        }
    }
}

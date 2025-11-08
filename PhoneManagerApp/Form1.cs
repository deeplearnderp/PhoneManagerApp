using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        private const int TerminalInputHeight = 28;
        private const int PromptColumnWidth = 18;
        private const float TerminalFontSize = 9.5f;
        private const int InnerPanelTopPadding = 10;
        private static readonly Color PromptColor = Color.FromArgb(0, 255, 0);
        private static readonly Color InputBackColor = Color.Black;
        private static readonly Color InputForeColor = Color.White;
        private static readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private static readonly Padding TerminalPadding = new Padding(4, 2, 4, 0);
        private const int MaxInputLines = 5;

        private string embeddedAdbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "platform-tools", "adb.exe");
        private const string SystemAdbPathConst = @"C:\\Users\\coryn\\Downloads\\platform-tools-latest-windows\\platform-tools\\adb.exe";
        private string adbConfigFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adb_config.txt");

        private AdbConnector adbConnector;
        private ComboBox comboMode;
        private ComboBox comboConnectionType;
        private ComboBox comboAdbSource;
        private Button btnConnectPhone;
        private Button btnScanDevices;
        private ComboBox comboDevices;
        private Button btnToggleNotifications;
        private Button btnToggleTerminal;
        private SplitContainer splitContainer;
        private RichTextBox rtbOutput;
        private Panel terminalPanel;
        private RichTextBox terminalOutput;
        private BottomAlignedTextBox terminalInput;
        private Label statusLabel;

        private readonly List<string> commandHistory = new();
        private int historyIndex = -1;

        // 👇 RichText internal padding support
        private const int EM_SETRECT = 0xB3;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, ref Rectangle rect);

        public Form1()
        {
            InitializeComponent();
            InitializeLayout();
            LoadAdbSourceConfig();
        }

        private void InitializeLayout()
        {
            this.BackColor = Color.White;
            this.Text = "Phone Manager App";
            this.MinimumSize = new Size(950, 600);

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 245)
            };

            comboMode = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            comboMode.Items.AddRange(new[] { "ADB Control Mode", "File Explorer (MTP)" });
            comboMode.SelectedIndex = 0;

            comboConnectionType = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            comboConnectionType.Items.AddRange(new[] { "USB", "Wi-Fi (Secure)" });
            comboConnectionType.SelectedIndex = 0;
            comboConnectionType.SelectedIndexChanged += ComboConnectionType_SelectedIndexChanged;

            comboAdbSource = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            comboAdbSource.Items.AddRange(new[] { "App Folder ADB", "System ADB" });
            comboAdbSource.SelectedIndex = 1;
            comboAdbSource.SelectedIndexChanged += (s, e) => SaveAdbSourceConfig();

            btnConnectPhone = new Button { Text = "Connect Phone", AutoSize = true };
            btnConnectPhone.Click += async (s, e) => await ConnectPhoneAsync();

            btnScanDevices = new Button { Text = "Scan Devices", AutoSize = true };
            btnScanDevices.Click += BtnScanDevices_Click;

            comboDevices = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };

            btnToggleNotifications = new Button { Text = "Toggle Notifications", AutoSize = true, Enabled = false };
            btnToggleNotifications.Click += BtnToggleNotifications_Click;

            btnToggleTerminal = new Button { Text = "Show Terminal", AutoSize = true };
            btnToggleTerminal.Click += (s, e) =>
            {
                splitContainer.Panel2Collapsed = !splitContainer.Panel2Collapsed;
                btnToggleTerminal.Text = splitContainer.Panel2Collapsed ? "Show Terminal" : "Hide Terminal";
            };

            statusLabel = new Label
            {
                Text = "Status: 🔴 Disconnected",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(30, 3, 0, 0),
                Padding = new Padding(10, 2, 0, 0)
            };

            topPanel.Controls.Add(comboMode);
            topPanel.Controls.Add(comboConnectionType);
            topPanel.Controls.Add(comboAdbSource);
            topPanel.Controls.Add(btnConnectPhone);
            topPanel.Controls.Add(btnScanDevices);
            topPanel.Controls.Add(comboDevices);
            topPanel.Controls.Add(btnToggleNotifications);
            topPanel.Controls.Add(btnToggleTerminal);
            topPanel.Controls.Add(statusLabel);
            Controls.Add(topPanel);

            // Expand form width to fit all controls
            topPanel.Layout += (s, e) =>
            {
                int totalWidth = 0;
                foreach (Control ctrl in topPanel.Controls)
                    totalWidth += ctrl.Width + ctrl.Margin.Horizontal;
                totalWidth += 120;
                if (this.Width < totalWidth)
                    this.Width = totalWidth;
            };

            // Add subtle separator under toolbar
            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(220, 220, 220) };
            Controls.Add(sep);
            sep.BringToFront();
            topPanel.BringToFront();

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(splitContainer);

            // Wrapped output with padding
            var outputHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 8, 10, 8)
            };
            splitContainer.Panel1.Controls.Add(outputHost);

            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            outputHost.Controls.Add(rtbOutput);

            // ✅ Apply internal padding fix
            SetRichTextPadding(rtbOutput, 6, 6, 6, 6);

            terminalPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                BorderStyle = BorderStyle.FixedSingle
            };
            splitContainer.Panel2.Controls.Add(terminalPanel);

            InitializeTerminalUI();
            splitContainer.Panel2Collapsed = true;
        }

        // 📏 Add padding to avoid top line clipping
        private void SetRichTextPadding(RichTextBox box, int left, int top, int right, int bottom)
        {
            var rect = box.ClientRectangle;
            rect.Inflate(-left, -top);
            rect.Offset(left, top);
            SendMessage(box.Handle, EM_SETRECT, 0, ref rect);
        }

        private void InitializeTerminalUI()
        {
            var titleLabel = new Label
            {
                Text = "ADB Terminal",
                ForeColor = Color.LightGray,
                BackColor = Color.FromArgb(35, 35, 35),
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            terminalPanel.Controls.Add(titleLabel);

            var innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6, InnerPanelTopPadding, 6, 6),
                BackColor = Color.FromArgb(25, 25, 25)
            };
            terminalPanel.Controls.Add(innerPanel);

            terminalOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(0, 255, 0),
                Font = new Font("Cascadia Mono", 9.5f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            innerPanel.Controls.Add(terminalOutput);

            var inputTable = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = TerminalInputHeight,
                ColumnCount = 2,
                BackColor = TerminalBackground,
                Padding = TerminalPadding
            };
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PromptColumnWidth));
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            innerPanel.Controls.Add(inputTable);

            var promptLabel = new Label
            {
                Text = ">",
                ForeColor = PromptColor,
                Font = new Font("Cascadia Mono", TerminalFontSize),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            inputTable.Controls.Add(promptLabel, 0, 0);

            terminalInput = new BottomAlignedTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = InputBackColor,
                ForeColor = InputForeColor,
                Font = new Font("Cascadia Mono", TerminalFontSize),
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical
            };
            inputTable.Controls.Add(terminalInput, 1, 0);

            terminalInput.KeyDown += TerminalInput_KeyDown;
            terminalInput.TextChanged += TerminalInput_TextChanged;
            terminalOutput.MouseDown += (s, e) => terminalInput.Focus();

            terminalOutput.AppendText("ADB Terminal Initialized\nType a command and press Enter.\n\n");
        }

        private void TerminalInput_TextChanged(object? sender, EventArgs e)
        {
            int lineCount = terminalInput.GetLineFromCharIndex(terminalInput.TextLength) + 1;
            lineCount = Math.Min(lineCount, MaxInputLines);
            int newHeight = TerminalInputHeight + (lineCount - 1) * 14;
            terminalInput.Parent.Height = newHeight;
        }

        private async void TerminalInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                string command = terminalInput.Text.Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    commandHistory.Add(command);
                    historyIndex = -1;
                    terminalOutput.AppendText($"> {command}\n");
                    terminalInput.Clear();
                    await ExecuteAdbCommandCaptureAsync(command);
                }
            }
        }

        private async void BtnScanDevices_Click(object sender, EventArgs e)
        {
            try
            {
                btnScanDevices.Enabled = false;
                UpdateStatus("Scanning…", Color.Goldenrod);
                rtbOutput.AppendText("[Scan] Starting scan...\n");

                var results = new List<string>();
                string list = await ExecuteAdbCommandCaptureAsync("devices");

                foreach (var line in list.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("List of devices")) continue;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('\t', ' ');
                    if (parts.Length >= 1 && line.Contains("device"))
                    {
                        string id = parts[0].Trim();
                        if (Regex.IsMatch(id, @"^\d{1,3}(\.\d{1,3}){3}(:\d+)?$"))
                            results.Add(id);
                        else if (!string.IsNullOrWhiteSpace(id))
                            results.Add(id);
                    }
                }

                if (results.Count > 0)
                {
                    comboDevices.Items.Clear();
                    foreach (var item in results)
                        comboDevices.Items.Add(item);
                    comboDevices.SelectedIndex = 0;
                    comboDevices.Visible = true;
                    btnScanDevices.Visible = false;
                    UpdateStatus("Devices found", Color.Green);
                    rtbOutput.AppendText($"[Scan] Found {results.Count} device(s).\n");
                }
                else
                {
                    UpdateStatus("No devices detected", Color.Red);
                    rtbOutput.AppendText("[Scan] No devices detected.\n");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Scan failed", Color.Red);
                rtbOutput.AppendText($"[Scan] Error: {ex.Message}\n");
            }
            finally
            {
                btnScanDevices.Enabled = true;
            }
        }

        private async Task ConnectPhoneAsync()
        {
            UpdateStatus("Connecting…", Color.Goldenrod);
            rtbOutput.AppendText("[Connect] Connecting...\n");

            try
            {
                var mode = comboConnectionType.SelectedItem?.ToString() ?? "USB";
                string? selected = comboDevices.Visible && comboDevices.SelectedItem != null
                    ? comboDevices.SelectedItem.ToString()
                    : null;

                if (mode == "Wi-Fi (Secure)")
                {
                    if (string.IsNullOrWhiteSpace(selected))
                    {
                        rtbOutput.AppendText("[Connect] No Wi-Fi device selected.\n");
                        UpdateStatus("Select device", Color.Red);
                        return;
                    }

                    string connectOut = await ExecuteAdbCommandCaptureAsync($"connect {selected}");
                    rtbOutput.AppendText(connectOut + "\n");

                    if (connectOut.Contains("connected to", StringComparison.OrdinalIgnoreCase) ||
                        connectOut.Contains("already connected", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateStatus($"Connected ({selected})", Color.Green);
                        btnToggleNotifications.Enabled = true;
                    }
                    else
                    {
                        UpdateStatus("Connection failed", Color.Red);
                    }
                }
                else
                {
                    string list = await ExecuteAdbCommandCaptureAsync("devices");
                    rtbOutput.AppendText(list + "\n");

                    if (list.Split('\n').Any(l => l.Contains("\tdevice")))
                    {
                        UpdateStatus("Connected (USB)", Color.Green);
                        btnToggleNotifications.Enabled = true;
                    }
                    else
                    {
                        UpdateStatus("No USB device", Color.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                rtbOutput.AppendText($"[Connect] Error: {ex.Message}\n");
                UpdateStatus("Connection error", Color.Red);
            }
        }

        private async Task<string> ExecuteAdbCommandCaptureAsync(string command)
        {
            try
            {
                string adbPath = GetSelectedAdbPath();
                var psi = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(adbPath) ?? AppDomain.CurrentDomain.BaseDirectory
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                await Task.Run(() => proc.WaitForExit());

                if (!string.IsNullOrWhiteSpace(stderr))
                    terminalOutput.AppendText(stderr + "\n");
                if (!string.IsNullOrWhiteSpace(stdout))
                    terminalOutput.AppendText(stdout + "\n");

                return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            }
            catch (Exception ex)
            {
                terminalOutput.AppendText($"Error: {ex.Message}\n");
                return $"Error: {ex.Message}";
            }
        }

        private void ComboConnectionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = comboConnectionType.SelectedItem?.ToString() ?? "USB";
            UpdateStatus($"Mode: {selected} Selected", selected == "USB" ? Color.Green : Color.DeepSkyBlue);
        }

        private void UpdateStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(text, color)));
                return;
            }

            statusLabel.Text = $"Status: {text}";
            statusLabel.ForeColor = color;
        }

        private string GetSelectedAdbPath()
        {
            var sel = comboAdbSource.SelectedItem?.ToString() ?? "System ADB";
            return sel == "App Folder ADB" ? embeddedAdbPath : SystemAdbPathConst;
        }

        private void SaveAdbSourceConfig()
        {
            try
            {
                var sel = comboAdbSource.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(sel))
                    File.WriteAllText(adbConfigFile, sel.Contains("App") ? "App" : "System");
            }
            catch { }
        }

        private void LoadAdbSourceConfig()
        {
            try
            {
                if (File.Exists(adbConfigFile))
                {
                    string val = File.ReadAllText(adbConfigFile).Trim();
                    comboAdbSource.SelectedIndex = (val.Equals("App", StringComparison.OrdinalIgnoreCase)) ? 0 : 1;
                }
            }
            catch { }
        }

        private async void BtnToggleNotifications_Click(object sender, EventArgs e)
        {
            if (adbConnector == null)
                adbConnector = new AdbConnector();

            bool success = await adbConnector.ToggleNotificationsAsync(enabled: false);
            MessageBox.Show(success ? "Notifications turned OFF." : "Failed to change notification settings.",
                success ? "Success" : "Error",
                MessageBoxButtons.OK,
                success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
    }
}

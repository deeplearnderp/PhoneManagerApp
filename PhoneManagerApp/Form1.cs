using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        // ================================
        // 🔧 TERMINAL UI SETTINGS (easy tuning)
        // ================================
        private const int TerminalInputHeight = 28;
        private const int PromptColumnWidth = 18;
        private const float TerminalFontSize = 9.5f;
        private const int PromptMarginTop = 1;
        private const int InnerPanelTopPadding = 10;
        private static readonly Color PromptColor = Color.FromArgb(0, 255, 0);
        private static readonly Color InputBackColor = Color.Black;
        private static readonly Color InputForeColor = Color.White;
        private static readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private static readonly Padding TerminalPadding = new Padding(4, 2, 4, 0);
        private const int MaxInputLines = 5;
        // ================================

        private AdbConnector adbConnector;
        private ComboBox comboMode;
        private ComboBox comboConnectionType;
        private Button btnConnectPhone;
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

        public Form1()
        {
            InitializeComponent();
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            this.BackColor = Color.White;
            this.Text = "Phone Manager App";
            this.MinimumSize = new Size(950, 600); // Prevent cutoff

            // --- Top control bar ---
            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false, // Prevent wrapping
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 245)
            };

            comboMode = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboMode.Items.AddRange(new[] { "ADB Control Mode", "File Explorer (MTP)" });
            comboMode.SelectedIndex = 0;

            // === Connection Type Dropdown ===
            comboConnectionType = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboConnectionType.Items.AddRange(new[] { "USB", "Wi-Fi (Secure)" });
            comboConnectionType.SelectedIndex = 0;
            comboConnectionType.SelectedIndexChanged += ComboConnectionType_SelectedIndexChanged;

            btnConnectPhone = new Button { Text = "Connect Phone", AutoSize = true };
            btnConnectPhone.Click += async (s, e) => await ConnectPhoneAsync();

            btnToggleNotifications = new Button { Text = "Toggle Notifications", AutoSize = true, Enabled = false };
            btnToggleNotifications.Click += BtnToggleNotifications_Click;

            btnToggleTerminal = new Button { Text = "Show Terminal", AutoSize = true };
            btnToggleTerminal.Click += (s, e) =>
            {
                splitContainer.Panel2Collapsed = !splitContainer.Panel2Collapsed;
                btnToggleTerminal.Text = splitContainer.Panel2Collapsed ? "Show Terminal" : "Hide Terminal";
            };

            // === Status Label ===
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
            topPanel.Controls.Add(btnConnectPhone);
            topPanel.Controls.Add(btnToggleNotifications);
            topPanel.Controls.Add(btnToggleTerminal);
            topPanel.Controls.Add(statusLabel);
            Controls.Add(topPanel);

            // === Ensure full toolbar visibility ===
            topPanel.Layout += (s, e) =>
            {
                int totalWidth = 0;
                foreach (Control ctrl in topPanel.Controls)
                    totalWidth += ctrl.Width + ctrl.Margin.Horizontal;

                totalWidth += 60; // buffer space for padding

                if (this.Width < totalWidth)
                    this.Width = totalWidth;
            };

            // --- Split container ---
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(45, 45, 45),
                IsSplitterFixed = false
            };
            Controls.Add(splitContainer);

            // --- Main output ---
            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9f),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };
            splitContainer.Panel1.Controls.Add(rtbOutput);

            // --- Terminal Panel ---
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

        private void ComboConnectionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = comboConnectionType.SelectedItem.ToString() ?? "USB";
            if (selected == "USB")
                UpdateStatus("Mode: USB Selected", Color.Green);
            else
                UpdateStatus("Mode: Wi-Fi Selected", Color.DeepSkyBlue);
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
                RowCount = 1,
                BackColor = TerminalBackground,
                Padding = TerminalPadding,
                Margin = new Padding(0)
            };
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PromptColumnWidth));
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            innerPanel.Controls.Add(inputTable);

            var promptLabel = new Label
            {
                Text = ">",
                ForeColor = PromptColor,
                Font = new Font("Cascadia Mono", TerminalFontSize, FontStyle.Regular, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, PromptMarginTop, 0, 0),
                AutoSize = false
            };
            inputTable.Controls.Add(promptLabel, 0, 0);

            terminalInput = new BottomAlignedTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = InputBackColor,
                ForeColor = InputForeColor,
                Font = new Font("Cascadia Mono", TerminalFontSize, FontStyle.Regular, GraphicsUnit.Point),
                Multiline = true,
                AcceptsReturn = true,
                Margin = new Padding(0),
                ScrollBars = ScrollBars.Vertical
            };
            inputTable.Controls.Add(terminalInput, 1, 0);

            terminalInput.KeyDown += TerminalInput_KeyDown;
            terminalInput.TextChanged += TerminalInput_TextChanged;
            terminalOutput.MouseDown += (s, e) => terminalInput.Focus();

            terminalOutput.AppendText("ADB Terminal Initialized\n");
            terminalOutput.AppendText("Type a command and press Enter.\n\n");
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
            if (e.KeyCode == Keys.Up)
            {
                if (commandHistory.Count > 0)
                {
                    if (historyIndex < commandHistory.Count - 1)
                        historyIndex++;
                    terminalInput.Text = commandHistory[commandHistory.Count - 1 - historyIndex];
                    terminalInput.SelectionStart = terminalInput.Text.Length;
                }
                e.SuppressKeyPress = true;
                return;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (historyIndex > 0)
                {
                    historyIndex--;
                    terminalInput.Text = commandHistory[commandHistory.Count - 1 - historyIndex];
                    terminalInput.SelectionStart = terminalInput.Text.Length;
                }
                else
                {
                    historyIndex = -1;
                    terminalInput.Clear();
                }
                e.SuppressKeyPress = true;
                return;
            }

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
                    await ExecuteAdbCommandAsync(command);
                }
            }
        }

        private async Task ExecuteAdbCommandAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            if (command.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                terminalOutput.Clear();
                terminalOutput.AppendText("Terminal cleared.\n\n");
                return;
            }

            try
            {
                string adbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "platform-tools", "adb.exe");
                if (!File.Exists(adbPath))
                {
                    terminalOutput.AppendText("ADB not found in platform-tools folder.\n");
                    return;
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = command,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Invoke(new Action(() =>
                        {
                            terminalOutput.AppendText(e.Data + "\n");
                            ScrollTerminalToBottom();
                        }));
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Invoke(new Action(() =>
                        {
                            terminalOutput.SelectionColor = Color.Red;
                            terminalOutput.AppendText(e.Data + "\n");
                            terminalOutput.SelectionColor = Color.FromArgb(0, 255, 0);
                            ScrollTerminalToBottom();
                        }));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await Task.Run(() => process.WaitForExit());
            }
            catch (Exception ex)
            {
                terminalOutput.AppendText($"Error executing ADB command: {ex.Message}\n");
                ScrollTerminalToBottom();
            }
        }

        private void ScrollTerminalToBottom()
        {
            terminalOutput.SelectionStart = terminalOutput.TextLength;
            terminalOutput.ScrollToCaret();
        }

        private async Task ConnectPhoneAsync()
        {
            adbConnector = new AdbConnector();
            rtbOutput.AppendText("Connecting to device...\n");
            bool connected = await adbConnector.ConnectAsync();
            if (connected)
            {
                rtbOutput.AppendText("Device connected successfully.\n");
                btnToggleNotifications.Enabled = true;
                UpdateStatus("Connected (USB)", Color.Green);
            }
            else
            {
                rtbOutput.AppendText("Failed to connect device.\n");
                UpdateStatus("Disconnected", Color.Red);
            }
        }

        private async void BtnToggleNotifications_Click(object sender, EventArgs e)
        {
            if (adbConnector == null) return;

            bool success = await adbConnector.ToggleNotificationsAsync(enabled: false);
            if (success)
                MessageBox.Show("Notifications turned OFF.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Failed to change notification settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}
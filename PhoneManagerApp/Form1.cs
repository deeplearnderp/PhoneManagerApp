using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhoneManagerApp.Core;

namespace PhoneManagerApp
{
    public partial class Form1 : Form
    {
        private AdbConnector adbConnector;
        private ComboBox comboMode;
        private Button btnConnectPhone;
        private Button btnToggleNotifications;
        private Button btnToggleTerminal;
        private SplitContainer splitContainer;
        private RichTextBox rtbOutput;
        private Panel terminalPanel;
        private RichTextBox terminalOutput;
        private TextBox terminalInput;

        public Form1()
        {
            InitializeComponent();
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            this.BackColor = Color.White;
            this.Text = "Phone Manager App";
            this.MinimumSize = new Size(800, 600);

            // --- Top control bar ---
            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
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

            topPanel.Controls.Add(comboMode);
            topPanel.Controls.Add(btnConnectPhone);
            topPanel.Controls.Add(btnToggleNotifications);
            topPanel.Controls.Add(btnToggleTerminal);
            Controls.Add(topPanel);

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

            // --- Main white output ---
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

            // --- Terminal Panel (bottom) ---
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
                Padding = new Padding(6),
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
                Height = 34,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(15, 15, 15),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inputTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            innerPanel.Controls.Add(inputTable);

            var promptLabel = new Label
            {
                Text = ">",
                ForeColor = Color.FromArgb(0, 255, 0),
                Font = new Font("Cascadia Mono", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            inputTable.Controls.Add(promptLabel, 0, 0);

            terminalInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Cascadia Mono", 9.5f),
                Multiline = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 6, 0, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            inputTable.Controls.Add(terminalInput, 1, 0);
            terminalInput.KeyDown += TerminalInput_KeyDown;
            terminalOutput.MouseDown += (s, e) => terminalInput.Focus();

            terminalOutput.AppendText("ADB Terminal Initialized\n");
            terminalOutput.AppendText("Type a command and press Enter.\n\n");
        }

        private async void TerminalInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string command = terminalInput.Text.Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    terminalOutput.AppendText($"> {command}\n");
                    terminalInput.Clear();
                    await ExecuteAdbCommandAsync(command);
                }
            }
        }

        private async Task ExecuteAdbCommandAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            // --- CLEAR COMMAND HANDLER ---
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
                    {
                        Invoke(new Action(() =>
                        {
                            terminalOutput.AppendText(e.Data + "\n");
                            ScrollTerminalToBottom();
                        }));
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Invoke(new Action(() =>
                        {
                            terminalOutput.SelectionColor = Color.Red;
                            terminalOutput.AppendText(e.Data + "\n");
                            terminalOutput.SelectionColor = Color.FromArgb(0, 255, 0);
                            ScrollTerminalToBottom();
                        }));
                    }
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
            }
            else
            {
                rtbOutput.AppendText("Failed to connect device.\n");
            }
        }

        private async void BtnToggleNotifications_Click(object sender, EventArgs e)
        {
            if (adbConnector == null) return;

            bool success = await adbConnector.ToggleNotificationsAsync(enabled: false);
            if (success)
            {
                MessageBox.Show("Notifications turned OFF.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to change notification settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

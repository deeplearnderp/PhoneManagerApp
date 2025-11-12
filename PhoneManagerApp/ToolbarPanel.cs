using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Represents the top toolbar with ADB controls, including
    /// Connect, Notifications, Terminal, Clear Output, and View Screen.
    /// </summary>
    public class ToolbarPanel : Panel
    {
        public event EventHandler ConnectPhoneClicked;
        public event EventHandler ToggleNotificationsClicked;
        public event EventHandler ToggleTerminalClicked;
        public event EventHandler ClearOutputClicked;

        public ComboBox ComboConnectionMode { get; private set; }
        public ComboBox ComboAdbSource { get; private set; }
        public ComboBox ComboDevices { get; private set; }

        public Button BtnConnectPhone { get; private set; }
        public Button BtnToggleNotifications { get; private set; }
        public Button BtnShowTerminal { get; private set; }
        public Button BtnClearOutput { get; private set; }
        public Button BtnViewScreen { get; private set; }

        private readonly ScreenMirrorService screenMirrorService = new();

        public ToolbarPanel()
        {
            Height = 38;
            Dock = DockStyle.Top;
            Padding = new Padding(10, 4, 10, 4);
            BackColor = Color.WhiteSmoke;

            InitializeControls();
        }

        private void InitializeControls()
        {
            // ================================
            // 🔌 Combo Boxes
            // ================================
            ComboConnectionMode = new ComboBox { Width = 140 };
            ComboConnectionMode.Items.Add("ADB Control Mode");
            ComboConnectionMode.SelectedIndex = 0;
            Controls.Add(ComboConnectionMode);

            ComboAdbSource = new ComboBox { Width = 140, Left = 150 };
            ComboAdbSource.Items.AddRange(new[] { "Wi-Fi (Secure)", "USB" });
            ComboAdbSource.SelectedIndex = 0;
            Controls.Add(ComboAdbSource);

            ComboDevices = new ComboBox { Width = 180, Left = 300 };
            ComboDevices.Items.Add("System ADB");
            ComboDevices.SelectedIndex = 0;
            Controls.Add(ComboDevices);

            // ================================
            // 🔘 Buttons
            // ================================
            BtnConnectPhone = CreateButton("Connect Phone", 490, 110, (_, _) => ConnectPhoneClicked?.Invoke(this, EventArgs.Empty));

            BtnToggleNotifications = CreateButton("Toggle Notifications", 610, 140, (_, _) => ToggleNotificationsClicked?.Invoke(this, EventArgs.Empty));

            BtnShowTerminal = CreateButton("Hide Terminal", 760, 110, (_, _) => ToggleTerminalClicked?.Invoke(this, EventArgs.Empty));

            BtnClearOutput = CreateButton("🖋 Clear Output", 880, 120, (_, _) => ClearOutputClicked?.Invoke(this, EventArgs.Empty));

            // ================================
            // 📱 View Screen (Live Stream)
            // ================================
            BtnViewScreen = CreateButton("View Screen", 1000, 110, async (_, _) =>
            {
                if (!screenMirrorService.IsRunning)
                {
                    BtnViewScreen.BackColor = Color.LimeGreen;
                    BtnViewScreen.ForeColor = Color.White;
                    BtnViewScreen.Text = "Stop Screen";
                    await screenMirrorService.StartAsync();
                }
                else
                {
                    screenMirrorService.Stop();
                    BtnViewScreen.BackColor = SystemColors.Control;
                    BtnViewScreen.ForeColor = Color.Black;
                    BtnViewScreen.Text = "View Screen";
                }
            });

            BtnViewScreen.BackColor = SystemColors.Control;
            BtnViewScreen.FlatStyle = FlatStyle.Flat;
            BtnViewScreen.FlatAppearance.BorderSize = 0;
        }

        private Button CreateButton(string text, int left, int width, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Left = left,
                Height = 26,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += onClick;
            Controls.Add(button);
            return button;
        }
    }
}

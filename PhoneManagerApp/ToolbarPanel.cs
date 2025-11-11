using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Represents the top toolbar with ADB controls (no AR or status labels).
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
            // Connection Mode ComboBox
            ComboConnectionMode = new ComboBox { Width = 140 };
            ComboConnectionMode.Items.Add("ADB Control Mode");
            ComboConnectionMode.SelectedIndex = 0;
            Controls.Add(ComboConnectionMode);

            // ADB Source ComboBox
            ComboAdbSource = new ComboBox { Width = 140, Left = 150 };
            ComboAdbSource.Items.AddRange(new[] { "Wi-Fi (Secure)", "USB" });
            ComboAdbSource.SelectedIndex = 0;
            Controls.Add(ComboAdbSource);

            // Device Source ComboBox
            ComboDevices = new ComboBox { Width = 180, Left = 300 };
            ComboDevices.Items.Add("System ADB");
            ComboDevices.SelectedIndex = 0;
            Controls.Add(ComboDevices);

            // Connect button
            BtnConnectPhone = CreateButton("Connect Phone", 490, 110, (_, _) => ConnectPhoneClicked?.Invoke(this, EventArgs.Empty));

            // Toggle notifications button
            BtnToggleNotifications = CreateButton("Toggle Notifications", 610, 140, (_, _) => ToggleNotificationsClicked?.Invoke(this, EventArgs.Empty));

            // Show/Hide Terminal button
            BtnShowTerminal = CreateButton("Hide Terminal", 760, 110, (_, _) => ToggleTerminalClicked?.Invoke(this, EventArgs.Empty));

            // Clear Output button
            BtnClearOutput = CreateButton("🖋 Clear Output", 880, 120, (_, _) => ClearOutputClicked?.Invoke(this, EventArgs.Empty));
        }

        private Button CreateButton(string text, int left, int width, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Left = left
            };
            button.Click += onClick;
            Controls.Add(button);
            return button;
        }
    }
}

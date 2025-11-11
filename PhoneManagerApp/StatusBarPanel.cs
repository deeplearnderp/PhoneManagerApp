using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Bottom status bar aligned like a footer. Left: connection.
    /// Right: Auto-Refresh label, AR button, Android/PC button, Last Update.
    /// </summary>
    public class StatusBarPanel : Panel
    {
        private readonly Label lblConnection;
        private readonly Label lblAutoRefresh;
        private readonly Label lblLastUpdated;
        private readonly Button btnAutoRefresh;
        private readonly Button btnTerminalMode;

        private bool autoRefreshEnabled = true;

        // nullable events to avoid non-null diagnostics
        public event EventHandler? AutoRefreshClicked;
        public event EventHandler? TerminalModeClicked;

        public StatusBarPanel()
        {
            Dock = DockStyle.Bottom;
            Height = 32;
            BackColor = Color.FromArgb(25, 25, 25);
            Padding = new Padding(10, 3, 10, 3);

            // Left-side connection label (docked left so it stays left)
            lblConnection = new Label
            {
                Text = "🔴 Disconnected",
                ForeColor = Color.Red,
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9)
            };
            Controls.Add(lblConnection);

            // Right-side flow: RightToLeft so everything hugs the right edge
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 3, 0, 0),
            };

            // Last Update (rightmost)
            lblLastUpdated = new Label
            {
                Text = "Last Update: —",
                ForeColor = Color.LightGray,
                AutoSize = true,
                Margin = new Padding(8, 5, 0, 0),
                Font = new Font("Segoe UI", 9)
            };

            // Android/PC button
            btnTerminalMode = new Button
            {
                Text = "Android",
                Width = 70,
                Height = 22,
                BackColor = Color.LimeGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 1, 0, 0)
            };
            btnTerminalMode.FlatAppearance.BorderSize = 0;
            btnTerminalMode.Click += (_, _) => TerminalModeClicked?.Invoke(this, EventArgs.Empty);

            // AR button
            btnAutoRefresh = new Button
            {
                Text = "AR",
                Width = 50,
                Height = 22,
                BackColor = Color.FromArgb(0, 180, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 1, 0, 0)
            };
            btnAutoRefresh.FlatAppearance.BorderSize = 0;
            btnAutoRefresh.Click += (_, _) => ToggleAutoRefreshInternal();

            // Auto-Refresh label (to the left of AR)
            lblAutoRefresh = new Label
            {
                Text = "Auto-Refresh: ON",
                ForeColor = Color.Lime,
                AutoSize = true,
                Margin = new Padding(8, 5, 0, 0),
                Font = new Font("Segoe UI", 9)
            };

            // Add to right-flow in reverse visual order (RTL)
            rightFlow.Controls.Add(lblLastUpdated);
            rightFlow.Controls.Add(btnTerminalMode);
            rightFlow.Controls.Add(btnAutoRefresh);
            rightFlow.Controls.Add(lblAutoRefresh);

            Controls.Add(rightFlow);
        }

        /// <summary>
        /// Called when user clicks the AR button. Updates visuals then fires event.
        /// </summary>
        private void ToggleAutoRefreshInternal()
        {
            SetAutoRefreshStatus(!autoRefreshEnabled);
            AutoRefreshClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Public setter to keep label + button in sync with MainWindow’s state.
        /// </summary>
        public void SetAutoRefreshStatus(bool enabled)
        {
            autoRefreshEnabled = enabled;

            if (autoRefreshEnabled)
            {
                lblAutoRefresh.Text = "Auto-Refresh: ON";
                lblAutoRefresh.ForeColor = Color.Lime;
                btnAutoRefresh.BackColor = Color.FromArgb(0, 180, 0);
            }
            else
            {
                lblAutoRefresh.Text = "Auto-Refresh: OFF";
                lblAutoRefresh.ForeColor = Color.Red;
                btnAutoRefresh.BackColor = Color.FromArgb(200, 30, 30);
            }
        }

        public void SetTerminalMode(bool androidMode)
        {
            btnTerminalMode.Text = androidMode ? "Android" : "PC";
            btnTerminalMode.BackColor = androidMode ? Color.LimeGreen : Color.RoyalBlue;
        }

        public void SetConnectionStatus(bool connected, string deviceName = "")
        {
            lblConnection.Text = connected
                ? $"🟢 Connected {(string.IsNullOrEmpty(deviceName) ? "" : $"({deviceName})")}"
                : "🔴 Disconnected";
            lblConnection.ForeColor = connected ? Color.Lime : Color.Red;
        }

        public void SetLastUpdated(DateTime time)
        {
            lblLastUpdated.Text = $"Last Update: {time:T}";
        }
    }
}

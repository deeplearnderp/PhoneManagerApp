#nullable disable
using System.Diagnostics;
using System.Text.RegularExpressions;
using PhoneManagerApp.Core;

namespace PhoneManagerApp.UI.Panels
{
    public class NotificationPanel : Panel
    {
        private FlowLayoutPanel _flowLayout;
        private Button _btnClearAll;
        private Label _titleLabel;

        public NotificationPanel()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);
            AutoScroll = true;
            BackColor = Color.WhiteSmoke;

            _titleLabel = new Label
            {
                Text = "📱 Active Notifications",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(4),
                ForeColor = Color.Black
            };

            _btnClearAll = new Button
            {
                Text = "🚮 Clear All",
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat
            };
            _btnClearAll.FlatAppearance.BorderSize = 0;
            _btnClearAll.Click += async (_, _) => await ClearAllNotificationsAsync();

            _flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(0),
            };

            Controls.Add(_flowLayout);
            Controls.Add(_btnClearAll);
            Controls.Add(_titleLabel);
        }

        // =======================================================
        // 🔄 Refresh Notifications
        // =======================================================
        public async Task RefreshNotificationsAsync(AdbConnector adb, TerminalPanel terminal)
        {
            if (adb == null || !adb.IsConnected)
            {
                terminal.AppendError("⚠️ Cannot load notifications — device not connected.");
                return;
            }

            try
            {
                terminal.AppendOutput("🔔 Fetching active notifications...");
                var output = await ExecuteAdbCommandAsync("shell dumpsys notification --noredact");

                DisplayNotifications(output);
                terminal.AppendOutput("✅ Notifications updated.");
            }
            catch (Exception ex)
            {
                terminal.AppendError($"⚠️ Failed to fetch notifications: {ex.Message}");
            }
        }

        // =======================================================
        // 📋 Display Notification Cards
        // =======================================================
        private void DisplayNotifications(string adbOutput)
        {
            _flowLayout.Controls.Clear();

            if (string.IsNullOrWhiteSpace(adbOutput))
            {
                _flowLayout.Controls.Add(new Label
                {
                    Text = "No active notifications.",
                    AutoSize = true,
                    Padding = new Padding(6),
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.Gray
                });
                return;
            }

            // Simple regex parsing for key info
            var notifications = ParseNotifications(adbOutput);

            if (notifications.Count == 0)
            {
                _flowLayout.Controls.Add(new Label
                {
                    Text = "No active notifications.",
                    AutoSize = true,
                    Padding = new Padding(6),
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.Gray
                });
                return;
            }

            foreach (var n in notifications)
            {
                var card = CreateNotificationCard(n);
                _flowLayout.Controls.Add(card);
            }
        }

        // =======================================================
        // 🧠 Parse Notifications
        // =======================================================
        private List<NotificationItem> ParseNotifications(string adbOutput)
        {
            var list = new List<NotificationItem>();

            // Split by notification sections
            var entries = adbOutput.Split("NotificationRecord", StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                var pkg = Regex.Match(entry, @"pkg=(\S+)").Groups[1].Value.Trim();
                var title = Regex.Match(entry, @"android\.title=(.+)").Groups[1].Value.Trim();
                var text = Regex.Match(entry, @"android\.text=(.+)").Groups[1].Value.Trim();
                var tag = Regex.Match(entry, @"tag=(\S+)").Groups[1].Value.Trim();
                var idMatch = Regex.Match(entry, @"id=(\d+)").Groups[1].Value.Trim();

                if (string.IsNullOrEmpty(pkg)) continue;

                list.Add(new NotificationItem
                {
                    Package = pkg,
                    Title = string.IsNullOrEmpty(title) ? "(No title)" : title,
                    Text = string.IsNullOrEmpty(text) ? "" : text,
                    Tag = tag,
                    Id = idMatch
                });
            }

            return list;
        }

        // =======================================================
        // 🪧 Notification Card UI
        // =======================================================
        private Panel CreateNotificationCard(NotificationItem item)
        {
            var card = new Panel
            {
                Width = _flowLayout.ClientSize.Width - 30,
                Height = 85,
                Margin = new Padding(4),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblPkg = new Label
            {
                Text = item.Package,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                AutoSize = false,
                Width = card.Width - 60,
                Location = new Point(8, 6)
            };

            var lblTitle = new Label
            {
                Text = item.Title,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AutoSize = false,
                Width = card.Width - 60,
                Location = new Point(8, 26)
            };

            var lblText = new Label
            {
                Text = item.Text,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                AutoSize = false,
                Width = card.Width - 60,
                Height = 30,
                Location = new Point(8, 46),
                ForeColor = Color.Gray
            };

            var btnClear = new Button
            {
                Text = "🗑 Clear",
                Width = 70,
                Height = 26,
                Location = new Point(card.Width - 78, card.Height - 32),
                BackColor = Color.FromArgb(250, 250, 250),
                FlatStyle = FlatStyle.Flat,
                Tag = item
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += async (_, _) => await ClearNotificationAsync(item);

            card.Controls.Add(lblPkg);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblText);
            card.Controls.Add(btnClear);

            return card;
        }

        // =======================================================
        // 🧹 Clear All Notifications
        // =======================================================
        private async Task ClearAllNotificationsAsync()
        {
            try
            {
                await ExecuteAdbCommandAsync("shell cmd notification cancel_all");
                _flowLayout.Controls.Clear();
                _flowLayout.Controls.Add(new Label
                {
                    Text = "All notifications cleared.",
                    AutoSize = true,
                    Padding = new Padding(6),
                    Font = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.Gray
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear notifications: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =======================================================
        // 🗑 Clear Individual Notification
        // =======================================================
        private async Task ClearNotificationAsync(NotificationItem item)
        {
            if (string.IsNullOrEmpty(item.Package) || string.IsNullOrEmpty(item.Id))
                return;

            string cmd = $"shell cmd notification cancel {item.Package} {item.Tag} {item.Id}";
            try
            {
                await ExecuteAdbCommandAsync(cmd);
                // remove card from panel
                var card = _flowLayout.Controls
                    .OfType<Panel>()
                    .FirstOrDefault(p => p.Controls.OfType<Button>().Any(b => b.Tag == item));

                if (card != null)
                    _flowLayout.Controls.Remove(card);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear notification: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =======================================================
        // ⚙️ Helper: Run ADB command
        // =======================================================
        private async Task<string> ExecuteAdbCommandAsync(string arguments)
        {
            var psi = new ProcessStartInfo("adb", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                Debug.WriteLine($"ADB Error: {error}");

            return output;
        }

        // =======================================================
        // 📦 Internal Data Class
        // =======================================================
        private class NotificationItem
        {
            public string Package { get; set; }
            public string Title { get; set; }
            public string Text { get; set; }
            public string Tag { get; set; }
            public string Id { get; set; }
        }
    }
}

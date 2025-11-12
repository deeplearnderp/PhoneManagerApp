using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;

namespace PhoneManagerApp
{
    public class DeviceInfoPanel : FlowLayoutPanel
    {
        private Label lblDeviceName;
        private Label lblIpAddress;
        private Label lblBattery;
        private Label lblWifi;
        private Label lblStorage;
        private Label lblLastUpdate;
        private Label lblStorageDetails;

        private bool storageExpanded = false;
        private int targetHeight = 0;
        private System.Windows.Forms.Timer expandTimer;

        public DeviceInfoPanel()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Color.White;
            Padding = new Padding(20, 8, 20, 8);
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;

            InitializeLabels();
            InitializeExpandAnimation();
        }

        private void InitializeLabels()
        {
            lblDeviceName = CreateLabel("Device:");
            lblIpAddress = CreateLabel("IP Address:");
            lblBattery = CreateLabel("Battery:");
            lblWifi = CreateLabel("Wi-Fi Signal:");
            lblStorage = CreateLabel("Storage:");
            lblLastUpdate = CreateLabel("Last Update:");

            lblStorage.Cursor = Cursors.Hand;
            lblStorage.Click += ToggleStorageExpanded;

            lblStorageDetails = new Label
            {
                AutoSize = false,
                Font = new Font("Consolas", 9, FontStyle.Regular),
                ForeColor = Color.Black,
                Height = 0,
                Width = 280,
                Visible = false,
                Margin = new Padding(25, 0, 0, 4)
            };

            Controls.AddRange(new Control[]
            {
                lblDeviceName,
                lblIpAddress,
                lblBattery,
                lblWifi,
                lblStorage,
                lblStorageDetails,
                lblLastUpdate
            });
        }

        private Label CreateLabel(string title)
        {
            return new Label
            {
                Text = $"{title} —",
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.Black,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        private void InitializeExpandAnimation()
        {
            expandTimer = new System.Windows.Forms.Timer { Interval = 15 };
            expandTimer.Tick += ExpandTimer_Tick;
        }

        private void ToggleStorageExpanded(object sender, EventArgs e)
        {
            storageExpanded = !storageExpanded;
            lblStorageDetails.Visible = true;

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
                if (lblStorageDetails.Height < targetHeight)
                    lblStorageDetails.Height += step;
                else
                    expandTimer.Stop();
            }
            else
            {
                if (lblStorageDetails.Height > targetHeight)
                    lblStorageDetails.Height -= step;
                else
                {
                    lblStorageDetails.Visible = false;
                    expandTimer.Stop();
                }
            }
        }

        public void UpdateDeviceInfo(string device, string ip, string battery, string wifi, string storage)
        {
            lblDeviceName.Text = $"Device: {device}";
            lblIpAddress.Text = $"IP Address: {ip}";
            lblBattery.Text = $"Battery: {battery}";
            lblWifi.Text = $"Wi-Fi Signal: {wifi}";
            lblStorage.Text = $"Storage: {storage}";
            lblLastUpdate.Text = $"Last Update: {DateTime.Now:T}";
        }

        // Add this at the top of your DeviceInfoPanel class:
        private Dictionary<string, double> lastStorageStats = new Dictionary<string, double>();
        private int lastUsedPercent = 0;
        private double lastUsedGB = 0;
        private double lastTotalGB = 0;
        private DateTime lastUpdatedTime = DateTime.MinValue;

        public void UpdateStorageBreakdown(string raw)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    lblStorageDetails.Text = "⚠ No storage data available.";
                    return;
                }

                // Try parsing new stats
                var matches = Regex.Matches(raw, @"(\w+)\s+Size:\s+(\d+)", RegexOptions.IgnoreCase);
                if (matches.Count == 0)
                {
                    lblStorageDetails.Text = "⚠ No detailed stats found in output.";
                    return;
                }

                var newStats = matches
                    .Cast<Match>()
                    .ToDictionary(
                        m => m.Groups[1].Value.Trim(),
                        m => double.Parse(m.Groups[2].Value) / 1_000_000_000 // Convert bytes to GB
                    );

                // Totals from the Data-Free line
                var dataLine = Regex.Match(raw, @"Data-Free:\s+(\d+)K\s+/\s+(\d+)K");
                double freeGB = 0, totalGB = 0;
                if (dataLine.Success)
                {
                    double freeK = double.Parse(dataLine.Groups[1].Value);
                    double totalK = double.Parse(dataLine.Groups[2].Value);
                    freeGB = freeK / 1024 / 1024;
                    totalGB = totalK / 1024 / 1024;
                }

                double usedGB = totalGB - freeGB;
                int usedPercent = totalGB > 0 ? (int)((usedGB / totalGB) * 100) : 0;

                // Cache successful result
                lastStorageStats = newStats;
                lastUsedPercent = usedPercent;
                lastUsedGB = usedGB;
                lastTotalGB = totalGB;
                lastUpdatedTime = DateTime.Now;

                // Apply colors
                lblStorage.ForeColor = usedPercent >= 90 ? Color.Red :
                    usedPercent >= 70 ? Color.DarkOrange :
                    Color.Green;

                lblStorage.Text =
                    $"Storage: {usedPercent}% used (Used: {usedGB:F1} GB / Total: {totalGB:F1} GB) {(storageExpanded ? "▾" : "▸")}";

                // Build detail list
                var sb = new StringBuilder();
                sb.AppendLine($"Apps:      {GetGB("App", newStats):F2} GB");
                sb.AppendLine($"Photos:    {GetGB("Photos", newStats):F2} GB");
                sb.AppendLine($"Videos:    {GetGB("Videos", newStats):F2} GB");
                sb.AppendLine($"Audio:     {GetGB("Audio", newStats):F2} GB");
                sb.AppendLine($"Downloads: {GetGB("Downloads", newStats):F2} GB");
                sb.AppendLine($"System:    {GetGB("System", newStats):F2} GB");
                sb.AppendLine($"Other:     {GetGB("Other", newStats):F2} GB");
                sb.AppendLine($"Free:      {freeGB:F2} GB");

                lblStorageDetails.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                // If parsing failed, fallback to cached data
                if (lastStorageStats?.Count > 0)
                {
                    lblStorage.ForeColor = lastUsedPercent >= 90 ? Color.Red :
                        lastUsedPercent >= 70 ? Color.DarkOrange :
                        Color.Green;

                    lblStorage.Text =
                        $"Storage: {lastUsedPercent}% used (Used: {lastUsedGB:F1} GB / Total: {lastTotalGB:F1} GB) {(storageExpanded ? "▾" : "▸")}";

                    var sb = new StringBuilder();
                    foreach (var kvp in lastStorageStats)
                        sb.AppendLine($"{kvp.Key,-10}: {kvp.Value:F2} GB");

                    lblStorageDetails.Text = sb.ToString();
                }
                else
                {
                    lblStorageDetails.Text = $"⚠ Failed to parse storage info: {ex.Message}";
                }
            }
        }

        private double GetGB(string key, Dictionary<string, double> stats)
        {
            return stats.TryGetValue(key, out double val) ? val : 0;
        }
    }
}

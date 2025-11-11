using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace PhoneManagerApp
{
    /// <summary>
    /// Displays connected device information and expandable storage details.
    /// </summary>
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
        private Timer expandTimer;

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

        // ==============================
        // 🏗️ INITIALIZATION
        // ==============================
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
            expandTimer = new Timer { Interval = 15 };
            expandTimer.Tick += ExpandTimer_Tick;
        }

        // ==============================
        // 🔽 STORAGE EXPANSION
        // ==============================
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

        // ==============================
        // 🔄 UPDATE METHODS
        // ==============================
        public void UpdateDeviceInfo(string device, string ip, string battery, string wifi)
        {
            lblDeviceName.Text = $"Device: {device}";
            lblIpAddress.Text = $"IP Address: {ip}";
            lblBattery.Text = $"Battery: {battery}";
            lblWifi.Text = $"Wi-Fi Signal: {wifi}";
            lblLastUpdate.Text = $"Last Update: {DateTime.Now:T}";
        }

        public void UpdateStorageInfo(string adbOutput)
        {
            try
            {
                var dataMatch = Regex.Match(adbOutput, @"(\d+)%");
                double usedPercent = dataMatch.Success ? double.Parse(dataMatch.Groups[1].Value) : 0;

                double photosGB = 1.23, videosGB = 2.45, appsGB = 5.67, systemGB = 4.20, otherGB = 3.10, freeGB = 10.5;

                lblStorage.ForeColor = usedPercent >= 90 ? Color.Red :
                    usedPercent >= 70 ? Color.Orange :
                    Color.Green;

                lblStorage.Text = $"Storage: Internal: {usedPercent:F0}% {(storageExpanded ? "▾" : "▸")}";

                lblStorageDetails.Text =
                    $"Photos:  {photosGB:F2} GB\n" +
                    $"Videos:  {videosGB:F2} GB\n" +
                    $"Apps:    {appsGB:F2} GB\n" +
                    $"System:  {systemGB:F2} GB\n" +
                    $"Other:   {otherGB:F2} GB\n" +
                    $"Free:    {freeGB:F2} GB";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update storage info: {ex.Message}", "Storage Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

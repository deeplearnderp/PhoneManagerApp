using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using PhoneManagerApp.Core;

namespace PhoneManagerApp
{
    /// <summary>
    /// Handles updating and displaying parsed device information
    /// (Battery, Wi-Fi, Storage, etc.) in the UI.
    /// </summary>
    public class DeviceInfoDisplay
    {
        private readonly Label lblDeviceName;
        private readonly Label lblDeviceIp;
        private readonly Label lblBattery;
        private readonly Label lblWifi;
        private readonly Label lblStorage;
        private readonly Label lblLastUpdate;

        public DeviceInfoDisplay(
            Label name,
            Label ip,
            Label battery,
            Label wifi,
            Label storage,
            Label last)
        {
            lblDeviceName = name;
            lblDeviceIp = ip;
            lblBattery = battery;
            lblWifi = wifi;
            lblStorage = storage;
            lblLastUpdate = last;
        }

        /// <summary>
        /// Updates all device info labels with fresh data parsed from ADB text output.
        /// </summary>
        /// <param name="text">Raw ADB device output.</param>
        /// <param name="model">Device model string.</param>
        /// <param name="ip">Connected device IP address.</param>
        public void Update(string text, string model, string ip)
        {
            try
            {
                // Device identifiers
                lblDeviceName.Text = $"Device: {model}";
                lblDeviceIp.Text = $"IP Address: {ip}";

                // Battery
                lblBattery.Text = $"Battery: {DeviceStatParser.ParseBattery(text)}";

                // Wi-Fi
                lblWifi.Text = $"Wi-Fi Signal: {DeviceStatParser.ParseWifi(text)}";

                // 🗄 Storage
                string storageText = DeviceStatParser.ParseStorage(text);

                // Avoid overwriting Form1’s expandable label arrow or tag
                if (!lblStorage.Text.Contains("▾") && !lblStorage.Text.Contains("▸"))
                    lblStorage.Text = $"Storage: Internal: {storageText}";

                // 🎨 Colorize storage text based on usage %
                var match = Regex.Match(storageText, @"(\d+)%");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
                {
                    lblStorage.ForeColor = percent >= 85 ? Color.OrangeRed :
                                           percent >= 70 ? Color.DarkOrange :
                                           Color.LightGreen;
                }
                else
                {
                    lblStorage.ForeColor = Color.White;
                }

                // Timestamp
                lblLastUpdate.Text = $"Last Update: {DeviceStatParser.ParseTimestamp()}";
            }
            catch (Exception ex)
            {
                lblLastUpdate.Text = $"Error updating info: {ex.Message}";
            }
        }
    }
}

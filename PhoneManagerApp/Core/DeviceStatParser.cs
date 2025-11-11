using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PhoneManagerApp.Core
{
    public static class DeviceStatParser
    {
        public static string ParseBattery(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "—";

            var match = Regex.Match(text, @"level[:=]\s*(\d+)%?");
            return match.Success ? $"{match.Groups[1].Value}%" : "—";
        }

        public static string ParseWifi(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "—";

            var match = Regex.Match(text, @"rssi[:=]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int rssi))
                return $"{rssi} dBm {GetSignalBars(rssi)}";

            return "—";
        }

        public static string ParseStorage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "—";

            string internalPercent = GetMountUsage(text, "/data");
            string sdPercent = GetMountUsage(text, "/sdcard");
            string emulatedPercent = GetMountUsage(text, "/storage/emulated/0");

            // Choose best available
            if (emulatedPercent != "—")
                internalPercent = emulatedPercent;

            if (internalPercent == "—" && sdPercent == "—")
                return "—";

            if (sdPercent != "—" && internalPercent != "—")
                return $"Internal: {internalPercent} | SD: {sdPercent}";
            if (internalPercent != "—")
                return $"Internal: {internalPercent}";
            if (sdPercent != "—")
                return $"SD: {sdPercent}";

            return "—";
        }

        private static string GetMountUsage(string text, string mount)
        {
            foreach (var line in text.Split('\n'))
            {
                if (line.Contains(mount, StringComparison.OrdinalIgnoreCase))
                {
                    // Typical line format: /dev/block/...  29G  26G  3G  90% /data
                    var match = Regex.Match(line, @"(\d+)%");
                    if (match.Success)
                        return $"{match.Groups[1].Value}%";
                }
            }
            return "—";
        }

        public static int ExtractPercentValue(string percentText)
        {
            if (int.TryParse(percentText.Replace("%", "").Trim(), out int val))
                return val;
            return -1;
        }

        private static string GetSignalBars(int rssi)
        {
            if (rssi >= -50) return "📶📶📶📶 Excellent";
            if (rssi >= -60) return "📶📶📶 Good";
            if (rssi >= -70) return "📶📶 Fair";
            if (rssi >= -80) return "📶 Weak";
            return "❌ No signal";
        }
        
        public static string ParseTimestamp()
        {
            return DateTime.Now.ToString("h:mm:ss tt");
        }

    }
}

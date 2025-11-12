using System.Text;
using System.Text.RegularExpressions;
using Timer = System.Windows.Forms.Timer;

namespace PhoneManagerApp.UI.Panels;

public class DeviceInfoPanel : FlowLayoutPanel
{
    private Timer _expandTimer;

    private Dictionary<string, double> _lastStorageStats = new();
    private double _lastTotalGb;
    private DateTime _lastUpdatedTime = DateTime.MinValue;
    private double _lastUsedGb;
    private int _lastUsedPercent;

    private Label _lblBattery;
    private PictureBox _batteryIcon;
    private Label _lblDeviceName;
    private Label _lblIpAddress;
    private Label _lblLastUpdate;
    private Label _lblStorage;
    private Label _lblStorageDetails;
    private Label _lblWifi;

    private bool _storageExpanded;
    private int _targetHeight;

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
        _lblDeviceName = CreateLabel("Device:");
        _lblIpAddress = CreateLabel("IP Address:");
        _lblBattery = CreateLabel("Battery:");
        _lblWifi = CreateLabel("Wi-Fi Signal:");
        _lblStorage = CreateLabel("Storage:");
        _lblLastUpdate = CreateLabel("Last Update:");

        // 🔋 Battery icon setup
        _batteryIcon = new PictureBox
        {
            Size = new Size(22, 22),
            SizeMode = PictureBoxSizeMode.Zoom,
            Visible = false,
            Margin = new Padding(5, 0, 0, 0)
        };

        // Group battery text + icon in a small horizontal layout
        var batteryPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 4)
        };
        batteryPanel.Controls.Add(_lblBattery);
        batteryPanel.Controls.Add(_batteryIcon);

        _lblStorage.Cursor = Cursors.Hand;
        _lblStorage.Click += ToggleStorageExpanded;

        _lblStorageDetails = new Label
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
            _lblDeviceName,
            _lblIpAddress,
            batteryPanel,
            _lblWifi,
            _lblStorage,
            _lblStorageDetails,
            _lblLastUpdate
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
        _expandTimer = new Timer { Interval = 15 };
        _expandTimer.Tick += ExpandTimer_Tick;
    }

    private void ToggleStorageExpanded(object sender, EventArgs e)
    {
        _storageExpanded = !_storageExpanded;
        _lblStorageDetails.Visible = true;

        var text = _lblStorage.Text.Replace("▸", "").Replace("▾", "").Trim();
        _lblStorage.Text = $"{text} {(_storageExpanded ? "▾" : "▸")}";

        _targetHeight = _storageExpanded ? 100 : 0;
        _expandTimer.Start();
    }

    private void ExpandTimer_Tick(object sender, EventArgs e)
    {
        var step = 10;
        if (_storageExpanded)
        {
            if (_lblStorageDetails.Height < _targetHeight)
                _lblStorageDetails.Height += step;
            else
                _expandTimer.Stop();
        }
        else
        {
            if (_lblStorageDetails.Height > _targetHeight)
                _lblStorageDetails.Height -= step;
            else
            {
                _lblStorageDetails.Visible = false;
                _expandTimer.Stop();
            }
        }
    }

    // ==========================================================
    // 🛰️ Display Device Info
    // ==========================================================
    public void UpdateDeviceInfo(string device, string ip, string battery, string wifi, string storage, string? extraIp = null, bool isCharging = false)
    {
        _lblDeviceName.Text = $"Device: {device}";

        var ipText = new StringBuilder();
        ipText.AppendLine($"IP Address: {ip}");
        if (!string.IsNullOrEmpty(extraIp) && extraIp != "—")
        {
            ipText.AppendLine($"Tailscale:  {extraIp}");
        }
        _lblIpAddress.Text = ipText.ToString();

        // 🔋 Battery with optional charging icon
        _lblBattery.Text = $"Battery: {battery}";
        if (isCharging)
        {
            _batteryIcon.Image = LoadEmbeddedImage("PhoneManagerApp.Resources.charging.png");
            _batteryIcon.Visible = true;
        }
        else
        {
            _batteryIcon.Visible = false;
        }

        _lblWifi.Text = $"Wi-Fi Signal: {wifi}";

        // Color code Wi-Fi strength
        if (wifi.Contains("Excellent"))
            _lblWifi.ForeColor = Color.Green;
        else if (wifi.Contains("Good"))
            _lblWifi.ForeColor = Color.LimeGreen;
        else if (wifi.Contains("Fair"))
            _lblWifi.ForeColor = Color.DarkOrange;
        else if (wifi.Contains("Weak"))
            _lblWifi.ForeColor = Color.Red;
        else
            _lblWifi.ForeColor = Color.Gray;

        _lblStorage.Text = $"Storage: {storage}";
        _lblLastUpdate.Text = $"Last Update: {DateTime.Now:T}";
    }

    // ==========================================================
    // 💾 Storage Breakdown (unchanged)
    // ==========================================================
    public void UpdateStorageBreakdown(string raw)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                _lblStorageDetails.Text = "⚠ No storage data available.";
                return;
            }

            var matches = Regex.Matches(raw, @"(\w+)\s+Size:\s+(\d+)", RegexOptions.IgnoreCase);
            if (matches.Count == 0)
            {
                _lblStorageDetails.Text = "⚠ No detailed stats found in output.";
                return;
            }

            var newStats = matches.ToDictionary(
                m => m.Groups[1].Value.Trim(),
                m => double.Parse(m.Groups[2].Value) / 1_000_000_000
            );

            var dataLine = Regex.Match(raw, @"Data-Free:\s+(\d+)K\s+/\s+(\d+)K");
            double freeGb = 0, totalGb = 0;
            if (dataLine.Success)
            {
                var freeK = double.Parse(dataLine.Groups[1].Value);
                var totalK = double.Parse(dataLine.Groups[2].Value);
                freeGb = freeK / 1024 / 1024;
                totalGb = totalK / 1024 / 1024;
            }

            var usedGb = totalGb - freeGb;
            var usedPercent = totalGb > 0 ? (int)(usedGb / totalGb * 100) : 0;

            _lastStorageStats = newStats;
            _lastUsedPercent = usedPercent;
            _lastUsedGb = usedGb;
            _lastTotalGb = totalGb;
            _lastUpdatedTime = DateTime.Now;

            _lblStorage.ForeColor = usedPercent >= 90 ? Color.Red :
                usedPercent >= 70 ? Color.DarkOrange :
                Color.Green;

            _lblStorage.Text =
                $"Storage: {usedPercent}% used (Used: {usedGb:F1} GB / Total: {totalGb:F1} GB) {(_storageExpanded ? "▾" : "▸")}";

            var sb = new StringBuilder();
            sb.AppendLine($"Apps:      {GetGb("App", newStats):F2} GB");
            sb.AppendLine($"Photos:    {GetGb("Photos", newStats):F2} GB");
            sb.AppendLine($"Videos:    {GetGb("Videos", newStats):F2} GB");
            sb.AppendLine($"Audio:     {GetGb("Audio", newStats):F2} GB");
            sb.AppendLine($"Downloads: {GetGb("Downloads", newStats):F2} GB");
            sb.AppendLine($"System:    {GetGb("System", newStats):F2} GB");
            sb.AppendLine($"Other:     {GetGb("Other", newStats):F2} GB");
            sb.AppendLine($"Free:      {freeGb:F2} GB");

            _lblStorageDetails.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            _lblStorageDetails.Text = $"⚠ Error: {ex.Message}";
        }
    }

    private double GetGb(string key, Dictionary<string, double> stats)
    {
        return stats.TryGetValue(key, out var val) ? val : 0;
    }

    private Image LoadEmbeddedImage(string resourcePath)
    {
        var assembly = typeof(DeviceInfoPanel).Assembly;
        using (var stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null) return null;
            return Image.FromStream(stream);
        }
    }
}

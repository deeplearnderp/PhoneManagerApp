namespace PhoneManagerApp;

/// <summary>
///     Bottom status bar aligned like a footer. Left: connection.
///     Right: Auto-Refresh label, AR button, Android/PC button, Last Update.
/// </summary>
public class StatusBarPanel : Panel
{
    private readonly Button _btnAutoRefresh;
    private readonly Button _btnTerminalMode;
    private readonly Label _lblAutoRefresh;
    private readonly Label _lblConnection;
    private readonly Label _lblLastUpdated;

    private bool _autoRefreshEnabled = true;

    public StatusBarPanel()
    {
        Dock = DockStyle.Bottom;
        Height = 32;
        BackColor = Color.FromArgb(25, 25, 25);
        Padding = new Padding(10, 3, 10, 3);

        // Left-side connection label (docked left so it stays left)
        _lblConnection = new Label
        {
            Text = "🔴 Disconnected",
            ForeColor = Color.Red,
            AutoSize = true,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9)
        };
        Controls.Add(_lblConnection);

        // Right-side flow: RightToLeft so everything hugs the right edge
        var rightFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 3, 0, 0)
        };

        // Last Update (rightmost)
        _lblLastUpdated = new Label
        {
            Text = "Last Update: —",
            ForeColor = Color.LightGray,
            AutoSize = true,
            Margin = new Padding(8, 5, 0, 0),
            Font = new Font("Segoe UI", 9)
        };

        // Android/PC button
        _btnTerminalMode = new Button
        {
            Text = "Android",
            Width = 70,
            Height = 22,
            BackColor = Color.LimeGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 1, 0, 0)
        };
        _btnTerminalMode.FlatAppearance.BorderSize = 0;
        _btnTerminalMode.Click += (_, _) => TerminalModeClicked?.Invoke(this, EventArgs.Empty);

        // AR button
        _btnAutoRefresh = new Button
        {
            Text = "AR",
            Width = 50,
            Height = 22,
            BackColor = Color.FromArgb(0, 180, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 1, 0, 0)
        };
        _btnAutoRefresh.FlatAppearance.BorderSize = 0;
        _btnAutoRefresh.Click += (_, _) => ToggleAutoRefreshInternal();

        // Auto-Refresh label (to the left of AR)
        _lblAutoRefresh = new Label
        {
            Text = "Auto-Refresh: ON",
            ForeColor = Color.Lime,
            AutoSize = true,
            Margin = new Padding(8, 5, 0, 0),
            Font = new Font("Segoe UI", 9)
        };

        // Add to right-flow in reverse visual order (RTL)
        rightFlow.Controls.Add(_lblLastUpdated);
        rightFlow.Controls.Add(_btnTerminalMode);
        rightFlow.Controls.Add(_btnAutoRefresh);
        rightFlow.Controls.Add(_lblAutoRefresh);

        Controls.Add(rightFlow);
    }

    // nullable events to avoid non-null diagnostics
    public event EventHandler? AutoRefreshClicked;
    public event EventHandler? TerminalModeClicked;

    /// <summary>
    ///     Called when user clicks the AR button. Updates visuals then fires event.
    /// </summary>
    private void ToggleAutoRefreshInternal()
    {
        SetAutoRefreshStatus(!_autoRefreshEnabled);
        AutoRefreshClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Public setter to keep label + button in sync with MainWindow’s state.
    /// </summary>
    public void SetAutoRefreshStatus(bool enabled)
    {
        _autoRefreshEnabled = enabled;

        if (_autoRefreshEnabled)
        {
            _lblAutoRefresh.Text = "Auto-Refresh: ON";
            _lblAutoRefresh.ForeColor = Color.Lime;
            _btnAutoRefresh.BackColor = Color.FromArgb(0, 180, 0);
        }
        else
        {
            _lblAutoRefresh.Text = "Auto-Refresh: OFF";
            _lblAutoRefresh.ForeColor = Color.Red;
            _btnAutoRefresh.BackColor = Color.FromArgb(200, 30, 30);
        }
    }

    public void SetTerminalMode(bool androidMode)
    {
        _btnTerminalMode.Text = androidMode ? "Android" : "PC";
        _btnTerminalMode.BackColor = androidMode ? Color.LimeGreen : Color.RoyalBlue;
    }

    public void SetConnectionStatus(bool connected, string deviceName = "")
    {
        _lblConnection.Text = connected
            ? $"🟢 Connected {(string.IsNullOrEmpty(deviceName) ? "" : $"({deviceName})")}"
            : "🔴 Disconnected";
        _lblConnection.ForeColor = connected ? Color.Lime : Color.Red;
    }

    public void SetLastUpdated(DateTime time)
    {
        _lblLastUpdated.Text = $"Last Update: {time:T}";
    }
}
namespace PhoneManagerApp.UI.Panels;

/// <summary>
///     Terminal panel with scrollable output, persistent command history,
///     color-coded messages, and autocomplete.
/// </summary>
public class TerminalPanel : Panel
{
    private const int MaxHistoryCount = 100;
    private readonly List<string> _commandHistory = new();

    private readonly string _historyFile;
    private readonly Color _inputBackground = Color.FromArgb(35, 35, 35);
    private readonly Color _inputTextColor = Color.White;

    private readonly Color _terminalBackground = Color.FromArgb(15, 15, 15);
    private readonly TextBox _terminalInput;
    private readonly RichTextBox _terminalOutput;
    private readonly Color _terminalTextColor = Color.Lime;
    private int _historyIndex = -1;

    public TerminalPanel()
    {
        Dock = DockStyle.Bottom;
        Height = 220;
        BackColor = _terminalBackground;
        BorderStyle = BorderStyle.None;

        // --- Ensure AppData folder ---
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhoneManagerApp"
        );
        Directory.CreateDirectory(appData);
        _historyFile = Path.Combine(appData, "command_history.txt");

        // --- Output Box ---
        _terminalOutput = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            BackColor = _terminalBackground,
            ForeColor = _terminalTextColor,
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        Controls.Add(_terminalOutput);

        // --- Input Box ---
        _terminalInput = new TextBox
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = _inputBackground,
            ForeColor = _inputTextColor,
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.FixedSingle
        };
        _terminalInput.KeyDown += TerminalInput_KeyDown;
        _terminalInput.PreviewKeyDown += TerminalInput_PreviewKeyDown;
        Controls.Add(_terminalInput);

        LoadHistory();
    }

    public event EventHandler<string> CommandEntered;

    // ===========================
    // 🔧 Input Handling
    // ===========================
    private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            var command = _terminalInput.Text.Trim();
            _terminalInput.Clear();

            if (string.IsNullOrWhiteSpace(command)) return;

            if (command.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                ClearOutput();
                return;
            }

            AppendOutput($"> {command}");
            AddToHistory(command);
            CommandEntered?.Invoke(this, command);
            _historyIndex = _commandHistory.Count;
            return;
        }

        if (e.Control && e.KeyCode == Keys.L)
        {
            e.SuppressKeyPress = true;
            ClearOutput();
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            e.SuppressKeyPress = true;
            NavigateHistory(-1);
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            e.SuppressKeyPress = true;
            NavigateHistory(1);
            return;
        }

        if (e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
            AutoCompleteCommand();
        }
    }

    private void TerminalInput_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        // Prevent Windows Forms from treating Tab as a focus key
        if (e.KeyCode == Keys.Tab)
            e.IsInputKey = true;
    }

    // ===========================
    // 💾 History Management
    // ===========================
    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFile))
            {
                var lines = File.ReadAllLines(_historyFile);
                _commandHistory.AddRange(lines.TakeLast(MaxHistoryCount));
            }
        }
        catch (Exception ex)
        {
            AppendWarning($"Failed to load command history: {ex.Message}");
        }
    }

    private void AddToHistory(string command)
    {
        if (_commandHistory.Count == 0 || _commandHistory.Last() != command)
            _commandHistory.Add(command);

        if (_commandHistory.Count > MaxHistoryCount)
            _commandHistory.RemoveAt(0);

        try
        {
            File.WriteAllLines(_historyFile, _commandHistory);
        }
        catch (Exception ex)
        {
            AppendWarning($"Failed to save command history: {ex.Message}");
        }
    }

    private void NavigateHistory(int direction)
    {
        if (_commandHistory.Count == 0) return;

        _historyIndex += direction;

        if (_historyIndex < 0)
        {
            _historyIndex = 0;
        }
        else if (_historyIndex >= _commandHistory.Count)
        {
            _historyIndex = _commandHistory.Count;
            _terminalInput.Clear();
            return;
        }

        _terminalInput.Text = _commandHistory[_historyIndex];
        _terminalInput.SelectionStart = _terminalInput.Text.Length;
    }

    private void AutoCompleteCommand()
    {
        var current = _terminalInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(current)) return;

        // Look for the most recent command that starts with the current text
        var match = _commandHistory
            .Where(c => c.StartsWith(current, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        // If no "starts with" match, try a "contains" match as fallback
        if (match == null)
            match = _commandHistory.LastOrDefault(c => c.Contains(current, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            _terminalInput.Text = match;
            _terminalInput.SelectionStart = match.Length;
        }
    }


    // ===========================
    // 🎨 Output Display
    // ===========================
    public void AppendOutput(string message)
    {
        AppendColoredLine(message, _terminalTextColor);
    }

    public void AppendInfo(string message)
    {
        AppendColoredLine(message, Color.Lime);
    }

    public void AppendWarning(string message)
    {
        AppendColoredLine($"⚠️ {message}", Color.Yellow);
    }

    public void AppendError(string message)
    {
        AppendColoredLine($"❌ {message}", Color.OrangeRed);
    }

    private void AppendColoredLine(string message, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendColoredLine(message, color));
            return;
        }

        _terminalOutput.SelectionStart = _terminalOutput.TextLength;
        _terminalOutput.SelectionColor = color;
        _terminalOutput.AppendText($"{message}\n");
        _terminalOutput.SelectionColor = _terminalTextColor;
        _terminalOutput.ScrollToCaret();
    }

    // ===========================
    // 🧹 Utility Methods
    // ===========================
    public void ClearOutput()
    {
        _terminalOutput.Clear();
    }

    public void ToggleVisibility()
    {
        Visible = !Visible;
    }

    public void FocusInput()
    {
        _terminalInput.Focus();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        // Allow the TextBox to capture the Tab key (and Arrow keys if needed)
        if (keyData == Keys.Tab)
            return true;

        return base.IsInputKey(keyData);
    }
}
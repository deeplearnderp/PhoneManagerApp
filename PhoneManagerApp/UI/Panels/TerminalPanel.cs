using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Terminal panel with scrollable output, persistent command history,
    /// color-coded messages, and autocomplete.
    /// </summary>
    public class TerminalPanel : Panel
    {
        private readonly RichTextBox terminalOutput;
        private readonly TextBox terminalInput;
        private readonly List<string> commandHistory = new();
        private int historyIndex = -1;

        private readonly string historyFile;
        private const int MaxHistoryCount = 100;

        public event EventHandler<string> CommandEntered;

        private readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private readonly Color TerminalTextColor = Color.Lime;
        private readonly Color InputBackground = Color.FromArgb(35, 35, 35);
        private readonly Color InputTextColor = Color.White;

        public TerminalPanel()
        {
            Dock = DockStyle.Bottom;
            Height = 220;
            BackColor = TerminalBackground;
            BorderStyle = BorderStyle.None;

            // --- Ensure AppData folder ---
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PhoneManagerApp"
            );
            Directory.CreateDirectory(appData);
            historyFile = Path.Combine(appData, "command_history.txt");

            // --- Output Box ---
            terminalOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                BackColor = TerminalBackground,
                ForeColor = TerminalTextColor,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(terminalOutput);

            // --- Input Box ---
            terminalInput = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = InputBackground,
                ForeColor = InputTextColor,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            terminalInput.KeyDown += TerminalInput_KeyDown;
            terminalInput.PreviewKeyDown += TerminalInput_PreviewKeyDown;
            Controls.Add(terminalInput);

            LoadHistory();
        }

        // ===========================
        // 🔧 Input Handling
        // ===========================
        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string command = terminalInput.Text.Trim();
                terminalInput.Clear();

                if (string.IsNullOrWhiteSpace(command)) return;

                if (command.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    ClearOutput();
                    return;
                }

                AppendOutput($"> {command}");
                AddToHistory(command);
                CommandEntered?.Invoke(this, command);
                historyIndex = commandHistory.Count;
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
                if (File.Exists(historyFile))
                {
                    var lines = File.ReadAllLines(historyFile);
                    commandHistory.AddRange(lines.TakeLast(MaxHistoryCount));
                }
            }
            catch (Exception ex)
            {
                AppendWarning($"Failed to load command history: {ex.Message}");
            }
        }

        private void AddToHistory(string command)
        {
            if (commandHistory.Count == 0 || commandHistory.Last() != command)
                commandHistory.Add(command);

            if (commandHistory.Count > MaxHistoryCount)
                commandHistory.RemoveAt(0);

            try
            {
                File.WriteAllLines(historyFile, commandHistory);
            }
            catch (Exception ex)
            {
                AppendWarning($"Failed to save command history: {ex.Message}");
            }
        }

        private void NavigateHistory(int direction)
        {
            if (commandHistory.Count == 0) return;

            historyIndex += direction;

            if (historyIndex < 0)
                historyIndex = 0;
            else if (historyIndex >= commandHistory.Count)
            {
                historyIndex = commandHistory.Count;
                terminalInput.Clear();
                return;
            }

            terminalInput.Text = commandHistory[historyIndex];
            terminalInput.SelectionStart = terminalInput.Text.Length;
        }

        private void AutoCompleteCommand()
        {
            string current = terminalInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(current)) return;

            // Look for the most recent command that starts with the current text
            var match = commandHistory
                .Where(c => c.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .LastOrDefault();

            // If no "starts with" match, try a "contains" match as fallback
            if (match == null)
                match = commandHistory.LastOrDefault(c => c.Contains(current, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                terminalInput.Text = match;
                terminalInput.SelectionStart = match.Length;
            }
        }


        // ===========================
        // 🎨 Output Display
        // ===========================
        public void AppendOutput(string message) => AppendColoredLine(message, TerminalTextColor);
        public void AppendInfo(string message) => AppendColoredLine(message, Color.Lime);
        public void AppendWarning(string message) => AppendColoredLine($"⚠️ {message}", Color.Yellow);
        public void AppendError(string message) => AppendColoredLine($"❌ {message}", Color.OrangeRed);

        private void AppendColoredLine(string message, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendColoredLine(message, color)));
                return;
            }

            terminalOutput.SelectionStart = terminalOutput.TextLength;
            terminalOutput.SelectionColor = color;
            terminalOutput.AppendText($"{message}\n");
            terminalOutput.SelectionColor = TerminalTextColor;
            terminalOutput.ScrollToCaret();
        }

        // ===========================
        // 🧹 Utility Methods
        // ===========================
        public void ClearOutput() => terminalOutput.Clear();

        public void ToggleVisibility() => Visible = !Visible;

        public void FocusInput() => terminalInput.Focus();
        
        protected override bool IsInputKey(Keys keyData)
        {
            // Allow the TextBox to capture the Tab key (and Arrow keys if needed)
            if (keyData == Keys.Tab)
                return true;

            return base.IsInputKey(keyData);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Terminal panel with scrollable output, command input, and persistent history.
    /// </summary>
    public class TerminalPanel : Panel
    {
        private readonly RichTextBox terminalOutput;
        private readonly TextBox terminalInput;

        public event EventHandler<string> CommandEntered;

        private readonly Color TerminalBackground = Color.FromArgb(15, 15, 15);
        private readonly Color TerminalTextColor = Color.Lime;
        private readonly Color InputBackground = Color.FromArgb(35, 35, 35);
        private readonly Color InputTextColor = Color.White;

        private readonly List<string> commandHistory = new();
        private int historyIndex = -1;
        private readonly string historyFilePath = Path.Combine(AppContext.BaseDirectory, "command_history.txt");

        public TerminalPanel()
        {
            Dock = DockStyle.Bottom;
            Height = 220;
            BackColor = TerminalBackground;
            BorderStyle = BorderStyle.None;

            // ----- Output Box -----
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

            // ----- Input Box -----
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
            Controls.Add(terminalInput);

            LoadCommandHistory();
        }

        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            // ENTER key — submit command
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string command = terminalInput.Text.Trim();
                terminalInput.Clear();

                if (string.IsNullOrWhiteSpace(command))
                    return;

                // store command in memory + file
                commandHistory.Add(command);
                historyIndex = commandHistory.Count;
                SaveCommandHistory();

                AppendOutput($"> {command}");
                CommandEntered?.Invoke(this, command);
                return;
            }

            // UP arrow — previous command
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;

                if (commandHistory.Count == 0)
                    return;

                historyIndex = Math.Max(0, historyIndex - 1);
                terminalInput.Text = commandHistory[historyIndex];
                terminalInput.SelectionStart = terminalInput.Text.Length;
                return;
            }

            // DOWN arrow — next command
            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;

                if (commandHistory.Count == 0)
                    return;

                historyIndex = Math.Min(commandHistory.Count, historyIndex + 1);

                if (historyIndex == commandHistory.Count)
                    terminalInput.Clear();
                else
                {
                    terminalInput.Text = commandHistory[historyIndex];
                    terminalInput.SelectionStart = terminalInput.Text.Length;
                }

                return;
            }
        }

        // ===========================
        // 🔧 Command History Helpers
        // ===========================

        private void LoadCommandHistory()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    var lines = File.ReadAllLines(historyFilePath);
                    commandHistory.AddRange(lines);
                    historyIndex = commandHistory.Count;
                }
            }
            catch (Exception ex)
            {
                AppendError($"⚠️ Failed to load command history: {ex.Message}");
            }
        }

        private void SaveCommandHistory()
        {
            try
            {
                File.WriteAllLines(historyFilePath, commandHistory);
            }
            catch (Exception ex)
            {
                AppendError($"⚠️ Failed to save command history: {ex.Message}");
            }
        }

        // ===========================
        // 🧠 Terminal Output Functions
        // ===========================

        public void AppendOutput(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendOutput(message)));
                return;
            }

            terminalOutput.AppendText($"{message}\n");
            terminalOutput.ScrollToCaret();
        }

        public void AppendError(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendError(message)));
                return;
            }

            terminalOutput.SelectionColor = Color.OrangeRed;
            terminalOutput.AppendText($"⚠️ {message}\n");
            terminalOutput.SelectionColor = TerminalTextColor;
            terminalOutput.ScrollToCaret();
        }

        public void ClearOutput()
        {
            terminalOutput.Clear();
            AppendOutput("🧹 Output cleared.");
        }

        public void ToggleVisibility()
        {
            Visible = !Visible;
        }

        public void FocusInput() => terminalInput.Focus();
    }
}

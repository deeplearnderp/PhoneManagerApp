using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// Terminal panel with scrollable output and command input.
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

            AppendOutput("ADB Terminal initialized.");
        }

        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            string command = terminalInput.Text.Trim();
            terminalInput.Clear();

            if (string.IsNullOrWhiteSpace(command))
                return;

            AppendOutput($"> {command}");
            CommandEntered?.Invoke(this, command);
        }

        // ===========================
        // 🔧 Public Methods
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

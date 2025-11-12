using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhoneManagerApp
{
    /// <summary>
    /// RichTextBox that wraps long lines, grows up to MaxVisibleLines,
    /// keeps a bottom bias, and never shows horizontal scrolling.
    /// </summary>
    public class BottomAlignedRichTextBox : RichTextBox
    {
        private const int EM_GETRECT = 0xB2;
        private const int EM_SETRECT = 0xB3;

        // how close text sits to the bottom visually (tweak to taste)
        public int BottomPadding { get; set; } = 4;

        // how many lines tall we allow before the parent starts scrolling
        public int MaxVisibleLines { get; set; } = 4;

        // base line height cache
        private int _lineHeight;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, ref RECT rect);

        public BottomAlignedRichTextBox()
        {
            BorderStyle = BorderStyle.None;
            DetectUrls = false;
            WordWrap = true;                              // ✅ wrap long lines
            ScrollBars = RichTextBoxScrollBars.None;      // no scrollbars here; parent handles scrolling
            Multiline = true;
            AcceptsTab = false;
            ShortcutsEnabled = true;
            AutoWordSelection = false;
            AutoSize = false;

            // this gives us the real content height as it changes
            ContentsResized += (_, e) => AdjustHeightFromContent(e.NewRectangle.Height);
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _lineHeight = (int)Font.GetHeight(CreateGraphics());
            AdjustTextRect();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AdjustTextRect();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _lineHeight = (int)Font.GetHeight(CreateGraphics());
            AdjustTextRect();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            // keep caret visible
            SelectionStart = TextLength;
            ScrollToCaret();
        }

        private void AdjustTextRect()
        {
            if (!IsHandleCreated) return;

            RECT rc = new RECT();
            SendMessage(Handle, EM_GETRECT, 0, ref rc);

            rc.Left = 3;
            rc.Right = ClientSize.Width - 3;

            // bias drawing rect toward the bottom so the last line feels anchored
            rc.Top = Math.Max(2, ClientSize.Height - _lineHeight - BottomPadding - 2);
            rc.Bottom = ClientSize.Height - 2;

            SendMessage(Handle, EM_SETRECT, 0, ref rc);
            Invalidate();
        }

        private void AdjustHeightFromContent(int contentPixelHeight)
        {
            if (contentPixelHeight <= 0) return;

            // approximate how many text lines the content currently spans
            int lines = Math.Max(1, (int)Math.Ceiling(contentPixelHeight / Math.Max(1f, _lineHeight)));
            int visibleLines = Math.Min(lines, MaxVisibleLines);

            int desired = visibleLines * _lineHeight + BottomPadding + 6; // +6 for small top/bottom margins
            if (desired < _lineHeight + BottomPadding + 6)
                desired = _lineHeight + BottomPadding + 6;

            if (Height != desired)
            {
                Height = desired;
                AdjustTextRect();
                // notify parent layout to reflow
                Parent?.PerformLayout();
            }
        }
    }
}

using System.Runtime.InteropServices;

namespace PhoneManagerApp;

/// <summary>
///     Custom TextBox that wraps text automatically, keeps it bottom-aligned,
///     and only scrolls vertically. Perfect for console-style input.
/// </summary>
public class BottomAlignedTextBox : TextBox
{
    private const int EmGetrect = 0xB2;
    private const int EmSetrect = 0xB3;
    private const int TextBottomOffset = 4; // Adjust vertical offset

    public BottomAlignedTextBox()
    {
        Multiline = true;
        WordWrap = true; // ✅ force wrapping (no horizontal scroll)
        ScrollBars = ScrollBars.Vertical; // ✅ vertical scroll only
        AcceptsReturn = true;
        AcceptsTab = false;
        BorderStyle = BorderStyle.None;
        ShortcutsEnabled = true; // allow copy/paste
        AutoSize = false; // stops height from resetting
    }

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, ref Rect rect);

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        AdjustTextRect();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        AdjustTextRect();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        AdjustTextRect();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();

        // Keep caret visible as you type
        SelectionStart = Text.Length;
        ScrollToCaret();
    }

    /// <summary>
    ///     Adjusts text area to stay visually aligned near the bottom.
    /// </summary>
    private void AdjustTextRect()
    {
        if (!IsHandleCreated) return;

        var rect = new Rect();
        SendMessage(Handle, EmGetrect, 0, ref rect);

        var lineHeight = (int)Font.GetHeight();

        rect.Left = 2;
        rect.Right = ClientSize.Width - 2;
        rect.Top = Math.Max(2, ClientSize.Height - (int)(lineHeight * 1.2) - TextBottomOffset);
        rect.Bottom = ClientSize.Height - 2;

        SendMessage(Handle, EmSetrect, 0, ref rect);
        Invalidate();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
}
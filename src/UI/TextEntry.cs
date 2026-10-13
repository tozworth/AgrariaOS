using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.UI;

// Shared editable text field for notes (title, body) and to-do lists (title,
// add-item box). Single-line mode submits on Enter; multi-line mode inserts a
// newline instead. Windows own persistence through Changed/Submitted.
internal sealed partial class TextEntry : IKeyboardSubscriber
{
    private const int TextLimit = 8192;
    private const int Padding = 6;
    private string text = "";
    private int caret;
    private int anchor;
    private int firstVisibleLine;
    private int firstCharacter;
    private char? pendingHighSurrogate;
    private bool selected;
    private bool revealCaret = true;
    private bool dragging;
    private bool caretAtRowEnd;
    private float? preferredCaretX;

    private int SelectionStart => Math.Min(anchor, caret);
    private int SelectionEnd => Math.Max(anchor, caret);
    private bool HasSelection => anchor != caret;

    internal bool Multiline { get; set; }
    internal bool WordWrap { get; set; }
    internal string Placeholder { get; set; } = "";
    internal Action<string>? Changed;
    internal Action? Submitted;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            if (!value)
                pendingHighSurrogate = null;
        }
    }

    internal string Text => text;

    // Window-driven rebind (e.g. selection changed). Never notifies, so typing
    // in one document can't echo into another.
    internal void Bind(string value)
    {
        text = value ?? "";
        if (text.Length > TextLimit)
            text = Truncate(text, TextLimit);
        caret = text.Length;
        anchor = caret;
        firstVisibleLine = 0;
        firstCharacter = 0;
        pendingHighSurrogate = null;
        dragging = false;
        revealCaret = true;
        caretAtRowEnd = false;
        preferredCaretX = null;
    }

    internal void ReceiveScroll(int delta)
    {
        if (Multiline && delta != 0)
        {
            firstVisibleLine = Math.Max(0, firstVisibleLine - Math.Sign(delta) * 3);
            revealCaret = false;
        }
    }

    public void RecieveTextInput(char inputChar)
    {
        if (!Selected)
            return;
        // Native text events deliver UTF-16 code units individually. Insert a pair together,
        // so the length limit can't leave half a character in the draft.
        if (char.IsHighSurrogate(inputChar))
        {
            pendingHighSurrogate = inputChar;
            return;
        }
        string value = char.IsLowSurrogate(inputChar)
            ? pendingHighSurrogate.HasValue ? new string(new[] { pendingHighSurrogate.Value, inputChar }) : ""
            : inputChar.ToString();
        pendingHighSurrogate = null;
        RecieveTextInput(value);
    }

    public void RecieveTextInput(string value)
    {
        if (!Selected || string.IsNullOrEmpty(value))
            return;
        value = Sanitize(value);
        int length = Math.Min(value.Length, TextLimit - text.Length + SelectionEnd - SelectionStart);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        if (length <= 0)
            return;
        int start = SelectionStart;
        text = text.Remove(start, SelectionEnd - start).Insert(start, value[..length]);
        caret = start;
        caret += length;
        anchor = caret;
        revealCaret = true;
        caretAtRowEnd = false;
        preferredCaretX = null;
        Changed?.Invoke(text);
    }

    public void RecieveCommandInput(char command)
    {
        if (!Selected)
            return;
        if (command == '\r')
        {
            if (Multiline)
                RecieveTextInput("\n");
            else
                Submitted?.Invoke();
        }
        else if (command == '\b')
        {
            if (!DeleteSelection() && caret > 0)
                DeleteRange(Previous(caret), caret);
        }
    }

    // SMAPI forwards navigation separately; native Windows text events don't include arrows.
    public void RecieveSpecialInput(Keys key) { }

    internal void Navigate(Keys key, bool extendSelection = false)
    {
        pendingHighSurrogate = null;
        switch (key)
        {
            case Keys.Left:
                caret = !extendSelection && HasSelection ? SelectionStart : Previous(caret);
                caretAtRowEnd = false;
                preferredCaretX = null;
                break;
            case Keys.Right:
                caret = !extendSelection && HasSelection ? SelectionEnd : Next(caret);
                caretAtRowEnd = false;
                preferredCaretX = null;
                break;
            case Keys.Home: MoveToRowEdge(end: false); break;
            case Keys.End: MoveToRowEdge(end: true); break;
            case Keys.Delete:
                if (!DeleteSelection() && caret < text.Length)
                    DeleteRange(caret, Next(caret));
                return;
            case Keys.Up when Multiline:
                MoveVertical(up: true);
                break;
            case Keys.Down when Multiline:
                MoveVertical(up: false);
                break;
            default:
                return;
        }
        if (!extendSelection)
            anchor = caret;
        revealCaret = true;
    }

    // Called before SMAPI suppresses the shortcut keys; the game dispatcher's
    // paste poll sees suppressed keys and must not insert the clipboard twice.
    internal bool ReceiveShortcut(Keys key)
    {
        if (key == Keys.A)
        {
            pendingHighSurrogate = null;
            anchor = 0;
            caret = text.Length;
            revealCaret = true;
            caretAtRowEnd = false;
            preferredCaretX = null;
            return true;
        }
        if (key is not (Keys.C or Keys.X or Keys.V))
            return false;
        pendingHighSurrogate = null;
        string pasted = "";
        bool succeeded;
        try
        {
            succeeded = key == Keys.V ? DesktopClipboard.GetText(ref pasted)
                : HasSelection && DesktopClipboard.SetText(text[SelectionStart..SelectionEnd]);
        }
        catch
        {
            // Clipboard availability can change outside the game. In particular,
            // a failed cut must leave the selected text and saved draft intact.
            return true;
        }
        if (succeeded && key == Keys.V)
            RecieveTextInput(pasted);
        else if (succeeded && key == Keys.X)
            DeleteSelection();
        return true;
    }

    private bool DeleteSelection()
    {
        if (!HasSelection)
            return false;
        DeleteRange(SelectionStart, SelectionEnd);
        return true;
    }

    private void DeleteRange(int start, int end)
    {
        text = text.Remove(start, end - start);
        anchor = caret = start;
        pendingHighSurrogate = null;
        revealCaret = true;
        caretAtRowEnd = false;
        preferredCaretX = null;
        Changed?.Invoke(text);
    }

    private void MoveVertical(bool up)
    {
        if (UsesWrapping)
        {
            MoveWrappedVertical(up);
            return;
        }
        int start = LineStart(caret);
        int column = caret - start;
        if (up)
        {
            if (start == 0)
                return;
            int previousEnd = start - 1;
            int previousStart = LineStart(previousEnd);
            caret = Math.Min(previousStart + column, previousEnd);
        }
        else
        {
            int end = LineEnd(caret);
            if (end == text.Length)
                return;
            int nextStart = end + 1;
            caret = Math.Min(nextStart + column, LineEnd(nextStart));
        }
        // Raw arithmetic can land between a surrogate pair; step past it.
        if (caret > 0 && caret < text.Length && char.IsLowSurrogate(text[caret]) && char.IsHighSurrogate(text[caret - 1]))
            caret++;
    }

    private string Sanitize(string value)
    {
        if (Multiline)
        {
            value = value.Replace("\r\n", "\n").Replace('\r', '\n');
            return string.Concat(value.Select(character => character == '\n' ? '\n'
                : char.IsControl(character) ? ' ' : character));
        }
        return string.Concat(value.Select(character => char.IsControl(character) ? ' ' : character));
    }

    private static string Truncate(string value, int length)
    {
        length = Math.Min(length, value.Length);
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..length];
    }

    private int Previous(int index) => index <= 0 ? 0
        : index > 1 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;
    private int Next(int index) => index >= text.Length ? text.Length
        : index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]) ? index + 2 : index + 1;
    private int LineStart(int index)
    {
        if (index <= 0)
            return 0;
        int newline = text.LastIndexOf('\n', index - 1);
        return newline + 1;
    }
    private int LineEnd(int index)
    {
        int newline = text.IndexOf('\n', index);
        return newline < 0 ? text.Length : newline;
    }
}

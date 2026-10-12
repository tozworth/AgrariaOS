using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.UI;

// Shared editable text field for notes (title, body) and to-do lists (title,
// add-item box). Single-line mode submits on Enter; multi-line mode inserts a
// newline instead. Windows own persistence through Changed/Submitted.
internal sealed class TextEntry : IKeyboardSubscriber
{
    private const int TextLimit = 8192;
    private const int Padding = 6;
    private string text = "";
    private int caret;
    private int firstVisibleLine;
    private int firstCharacter;
    private char? pendingHighSurrogate;
    private bool selected;

    internal bool Multiline { get; set; }
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
        firstVisibleLine = 0;
        firstCharacter = 0;
        pendingHighSurrogate = null;
    }

    internal void ReceiveScroll(int delta)
    {
        if (Multiline && delta != 0)
            firstVisibleLine = Math.Max(0, firstVisibleLine - Math.Sign(delta) * 3);
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
        int length = Math.Min(value.Length, TextLimit - text.Length);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        if (length <= 0)
            return;
        text = text.Insert(caret, value[..length]);
        caret += length;
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
        else if (command == '\b' && caret > 0)
        {
            int previous = Previous(caret);
            text = text.Remove(previous, caret - previous);
            caret = previous;
            Changed?.Invoke(text);
        }
    }

    // SMAPI forwards navigation separately; native Windows text events don't include arrows.
    public void RecieveSpecialInput(Keys key) { }

    internal void Navigate(Keys key)
    {
        switch (key)
        {
            case Keys.Left: caret = Previous(caret); break;
            case Keys.Right: caret = Next(caret); break;
            case Keys.Home: caret = LineStart(caret); break;
            case Keys.End: caret = LineEnd(caret); break;
            case Keys.Delete when caret < text.Length:
                text = text.Remove(caret, Next(caret) - caret);
                Changed?.Invoke(text);
                break;
            case Keys.Up when Multiline:
                MoveVertical(up: true);
                break;
            case Keys.Down when Multiline:
                MoveVertical(up: false);
                break;
        }
    }

    private void MoveVertical(bool up)
    {
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
    private int LineIndex(int index)
    {
        int line = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }

    internal void Draw(SpriteBatch batch, Rectangle bounds, ModConfig config)
    {
        int lineHeight = UiText.LineHeight(config.FontScale);
        if (bounds.Width < 16 || bounds.Height < lineHeight)
            return;
        FormControls.Field(batch, bounds, config, focused: Selected);
        int width = bounds.Width - Padding * 2;
        string[] lines = text.Split('\n');
        int caretLine = LineIndex(caret);
        int visibleLines = Math.Max(1, (bounds.Height - 4) / lineHeight);
        firstVisibleLine = Math.Clamp(firstVisibleLine, 0, Math.Max(0, lines.Length - visibleLines));
        if (caretLine < firstVisibleLine)
            firstVisibleLine = caretLine;
        else if (caretLine >= firstVisibleLine + visibleLines)
            firstVisibleLine = caretLine - visibleLines + 1;
        // Single-line fields center their only row; multi-line fields start at the top.
        int topPad = Multiline ? 2 : (bounds.Height - lineHeight) / 2;
        if (text.Length == 0)
        {
            UiText.Draw(batch, UiText.Fit(Placeholder, width, config.FontScale),
                new Vector2(bounds.X + Padding, bounds.Y + topPad), UiStyle.Muted, config);
            if (Selected && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000 < 500)
                batch.Draw(Game1.staminaRect, new Rectangle(bounds.X + Padding, bounds.Y + topPad, 1, lineHeight - 2),
                    UiStyle.Primary * config.Opacity);
            return;
        }
        int lastLine = Math.Min(lines.Length, firstVisibleLine + visibleLines);
        for (int i = firstVisibleLine; i < lastLine; i++)
        {
            float y = bounds.Y + topPad + (i - firstVisibleLine) * lineHeight;
            if (i != caretLine)
            {
                UiText.Draw(batch, UiText.Fit(lines[i], width, config.FontScale),
                    new Vector2(bounds.X + Padding, y), UiStyle.Primary, config);
                continue;
            }
            // The caret's line scrolls horizontally like a single-line field; other
            // lines clip with an ellipsis since they aren't being edited.
            int lineStart = LineStart(caret);
            int lineEnd = LineEnd(caret);
            firstCharacter = Math.Clamp(firstCharacter, lineStart, caret);
            while (firstCharacter < caret && Game1.smallFont.MeasureString(text[firstCharacter..caret]).X * config.FontScale > width - 2)
                firstCharacter = Next(firstCharacter);
            while (firstCharacter > lineStart && Game1.smallFont.MeasureString(text[Previous(firstCharacter)..caret]).X * config.FontScale <= width - 2)
                firstCharacter = Previous(firstCharacter);
            int end = firstCharacter;
            while (end < lineEnd && Game1.smallFont.MeasureString(text[firstCharacter..Next(end)]).X * config.FontScale <= width)
                end = Next(end);
            UiText.Draw(batch, text[firstCharacter..end], new Vector2(bounds.X + Padding, y), UiStyle.Primary, config);
            if (Selected && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000 < 500)
            {
                int x = bounds.X + Padding + (int)MathF.Round(Game1.smallFont.MeasureString(text[firstCharacter..caret]).X * config.FontScale);
                batch.Draw(Game1.staminaRect, new Rectangle(x, (int)MathF.Round(y), 1, lineHeight - 2),
                    UiStyle.Primary * config.Opacity);
            }
        }
    }
}

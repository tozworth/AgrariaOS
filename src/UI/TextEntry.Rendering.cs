using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

partial class TextEntry
{
    internal void BeginSelection(Point cursor, Rectangle bounds, ModConfig config)
    {
        pendingHighSurrogate = null;
        MoveToPointer(cursor, bounds, config, dragging: false);
        anchor = caret;
        dragging = true;
        revealCaret = true;
    }

    internal void DragSelection(Point cursor, Rectangle bounds, ModConfig config)
    {
        if (!dragging || bounds.IsEmpty)
            return;
        MoveToPointer(cursor, bounds, config, dragging: true);
        revealCaret = true;
    }

    internal void EndSelection() => dragging = false;

    private void MoveToPointer(Point cursor, Rectangle bounds, ModConfig config, bool dragging)
    {
        EnsureLayout(bounds, config);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int topPad = Multiline ? 2 : (bounds.Height - lineHeight) / 2;
        int line = Multiline
            ? firstVisibleLine + (int)MathF.Floor((cursor.Y - bounds.Y - topPad) / (float)lineHeight)
            : 0;
        int visibleLines = Math.Max(1, (bounds.Height - 4) / lineHeight);
        // Outside the field, advance one visual row per drag update and reveal it.
        line = Math.Clamp(line, Math.Max(0, firstVisibleLine - (dragging ? 1 : 0)),
            firstVisibleLine + visibleLines - (dragging ? 0 : 1));
        line = Math.Clamp(line, 0, rows.Count - 1);
        TextRow row = rows[line];
        (int start, int end) = VisibleRange(row, config);
        float x = cursor.X - bounds.X - Padding;
        int index = IndexAtX(start, end, x);
        if (!UsesWrapping && dragging)
        {
            if (x < 0 && start > row.Start)
                index = Previous(start);
            else if (cursor.X >= bounds.Right - Padding && end < row.End)
                index = Next(end);
        }
        // Inactive rows were drawn from their beginning. Keep that viewport when
        // a click/drag changes rows, including before the next draw updates it.
        if (LineStart(index) != LineStart(caret))
            firstCharacter = LineStart(index);
        caret = index;
        caretAtRowEnd = index == row.End;
        preferredCaretX = null;
    }

    // Rendering and mouse hit testing use the same glyph ranges. Editable fields
    // clip to whole characters rather than showing an unselectable ellipsis.
    private (int Start, int End) VisibleRange(TextRow row, ModConfig config)
    {
        if (UsesWrapping)
            return (row.Start, row.End);
        int lineStart = row.Start;
        int lineEnd = row.End;
        int start = lineStart == LineStart(caret) ? Math.Clamp(firstCharacter, lineStart, caret) : lineStart;
        if (start > lineStart && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
            start--;
        int end = start;
        while (end < lineEnd && UiText.Measure(text[start..Next(end)]).X * config.FontScale <= contentWidth)
            end = Next(end);
        return (start, end);
    }

    internal void Draw(SpriteBatch batch, Rectangle bounds, ModConfig config)
    {
        int lineHeight = UiText.LineHeight(config.FontScale);
        if (bounds.Width < 16 || bounds.Height < lineHeight)
            return;
        EnsureLayout(bounds, config);
        FormControls.Field(batch, bounds, config, focused: Selected);
        int width = contentWidth;
        int caretLine = CaretRow();
        int visibleLines = Math.Max(1, (bounds.Height - 4) / lineHeight);
        firstVisibleLine = Math.Clamp(firstVisibleLine, 0, Math.Max(0, rows.Count - visibleLines));
        if (revealCaret)
        {
            if (caretLine < firstVisibleLine)
                firstVisibleLine = caretLine;
            else if (caretLine >= firstVisibleLine + visibleLines)
                firstVisibleLine = caretLine - visibleLines + 1;
        }
        revealCaret = false;
        if (!UsesWrapping)
        {
            int caretStart = LineStart(caret);
            firstCharacter = Math.Clamp(firstCharacter, caretStart, caret);
            if (firstCharacter > caretStart && firstCharacter < text.Length
                && char.IsLowSurrogate(text[firstCharacter]) && char.IsHighSurrogate(text[firstCharacter - 1]))
                firstCharacter--;
            while (firstCharacter < caret && UiText.Measure(text[firstCharacter..caret]).X * config.FontScale > width - 2)
                firstCharacter = Next(firstCharacter);
            while (firstCharacter > caretStart && UiText.Measure(text[Previous(firstCharacter)..caret]).X * config.FontScale <= width - 2)
                firstCharacter = Previous(firstCharacter);
        }

        // Single-line fields center their row; multi-line fields start at the top.
        int topPad = Multiline ? 2 : (bounds.Height - lineHeight) / 2;
        if (text.Length == 0)
            UiText.Draw(batch, UiText.Fit(Placeholder, width, config.FontScale),
                new Vector2(bounds.X + Padding, bounds.Y + topPad), UiStyle.Muted, config);

        int lastLine = Math.Min(rows.Count, firstVisibleLine + visibleLines);
        for (int line = firstVisibleLine; line < lastLine; line++)
        {
            TextRow row = rows[line];
            int lineStart = row.Start;
            int lineEnd = row.End;
            int y = bounds.Y + topPad + (line - firstVisibleLine) * lineHeight;
            (int start, int end) = VisibleRange(row, config);
            if (Selected && HasSelection && SelectionStart <= lineEnd && SelectionEnd > lineStart)
            {
                int from = Math.Clamp(SelectionStart, start, end);
                int to = Math.Clamp(SelectionEnd, start, end);
                int left = (int)MathF.Round(UiText.Measure(text[start..from]).X * config.FontScale);
                int right = (int)MathF.Round(UiText.Measure(text[start..to]).X * config.FontScale);
                // A selected newline gets a small block, including on empty rows.
                if (end == lineEnd && row.HasNewline && SelectionEnd > lineEnd)
                    right = Math.Min(width, right + 4);
                if (right > left)
                    batch.Draw(Game1.staminaRect, new Rectangle(bounds.X + Padding + left, y, right - left, lineHeight - 2),
                        new Color(54, 101, 129) * config.Opacity);
            }
            UiText.Draw(batch, text[start..end], new Vector2(bounds.X + Padding, y), UiStyle.Primary, config);
            if (Selected && line == caretLine && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000 < 500)
            {
                int x = bounds.X + Padding + (int)MathF.Round(UiText.Measure(text[start..caret]).X * config.FontScale);
                batch.Draw(Game1.staminaRect, new Rectangle(x, y, 1, lineHeight - 2), UiStyle.Primary * config.Opacity);
            }
        }
    }
}

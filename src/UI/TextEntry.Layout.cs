using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgrariaOS.UI;

partial class TextEntry
{
    // Rows retain source offsets, including spaces. Only explicit newlines are
    // stored/copied; wrapping is a view of the draft at the current field width.
    private readonly record struct TextRow(int Start, int End, bool HasNewline);
    private readonly List<TextRow> rows = new();
    private int contentWidth;
    private float layoutScale = 1f;
    private string? layoutText;
    private int cachedWidth;
    private float cachedScale;
    private bool cachedWrap;
    private SpriteFont? cachedFont;
    private float cachedFontSpacing;

    private bool UsesWrapping => Multiline && WordWrap && contentWidth > 0;

    private void EnsureLayout(Rectangle bounds, ModConfig config)
    {
        contentWidth = Math.Max(0, bounds.Width - Padding * 2);
        layoutScale = config.FontScale;
        EnsureRows();
    }

    private void EnsureRows()
    {
        SpriteFont? font = StardewValley.Game1.smallFont;
        if (layoutText == text && cachedWidth == contentWidth && cachedScale == layoutScale
            && cachedWrap == UsesWrapping && ReferenceEquals(cachedFont, font)
            && cachedFontSpacing == (font?.Spacing ?? 0))
            return;

        rows.Clear();
        revealCaret = true;
        preferredCaretX = null;
        int start = 0;
        while (true)
        {
            int end = LineEnd(start);
            if (!UsesWrapping || start == end)
                rows.Add(new TextRow(start, end, end < text.Length));
            else
            {
                int rowStart = start;
                while (rowStart < end)
                {
                    int fit = rowStart;
                    int wordBreak = rowStart;
                    while (fit < end)
                    {
                        int next = Next(fit);
                        // Reserve room for the caret and avoid splitting UTF-16 pairs.
                        if (UiText.Measure(text[rowStart..next]).X * layoutScale > contentWidth - 2)
                            break;
                        fit = next;
                        if (char.IsWhiteSpace(text[fit - 1]))
                            wordBreak = fit;
                    }
                    // Long words fall back to character wrapping. Even an oversized
                    // glyph must advance the source offset so layout always terminates.
                    int rowEnd = fit == end ? end : wordBreak > rowStart ? wordBreak
                        : fit > rowStart ? fit : Next(rowStart);
                    rows.Add(new TextRow(rowStart, rowEnd, rowEnd == end && end < text.Length));
                    rowStart = rowEnd;
                }
            }
            if (end == text.Length)
                break;
            start = end + 1;
        }
        layoutText = text;
        cachedWidth = contentWidth;
        cachedScale = layoutScale;
        cachedWrap = UsesWrapping;
        cachedFont = font;
        cachedFontSpacing = font?.Spacing ?? 0;
    }

    private int CaretRow()
    {
        EnsureRows();
        for (int i = 0; i < rows.Count; i++)
        {
            TextRow row = rows[i];
            if (caret < row.End || caret == row.End
                && (row.HasNewline || i == rows.Count - 1 || caretAtRowEnd))
                return i;
        }
        return rows.Count - 1;
    }

    private void MoveToRowEdge(bool end)
    {
        if (UsesWrapping)
        {
            TextRow row = rows[CaretRow()];
            caret = end ? row.End : row.Start;
        }
        else
            caret = end ? LineEnd(caret) : LineStart(caret);
        caretAtRowEnd = end;
        preferredCaretX = null;
    }

    private void MoveWrappedVertical(bool up)
    {
        int current = CaretRow();
        TextRow row = rows[current];
        preferredCaretX ??= UiText.Measure(text[row.Start..caret]).X * layoutScale;
        int target = Math.Clamp(current + (up ? -1 : 1), 0, rows.Count - 1);
        if (target == current)
            return;
        row = rows[target];
        caret = IndexAtX(row.Start, row.End, preferredCaretX.Value);
        caretAtRowEnd = caret == row.End;
    }

    private int IndexAtX(int start, int end, float x)
    {
        float previousWidth = 0;
        for (int index = start; index < end;)
        {
            int next = Next(index);
            float nextWidth = UiText.Measure(text[start..next]).X * layoutScale;
            if (x < (previousWidth + nextWidth) / 2)
                return index;
            previousWidth = nextWidth;
            index = next;
        }
        return end;
    }
}

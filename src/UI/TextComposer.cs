using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.UI;

// Uses native text events for keyboard layouts/repeat, with our own pixel-aligned drawing.
internal sealed class TextComposer : IKeyboardSubscriber
{
    private const int TextLimit = 2048;
    private readonly Action<string> submit;
    private readonly string placeholder;
    private readonly bool clearOnSubmit;
    private string text = "";
    private int caret;
    private int firstCharacter;
    private char? pendingHighSurrogate;
    private bool selected;
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
    internal bool CanSubmit => !string.IsNullOrWhiteSpace(text);

    internal TextComposer(Action<string> submit, string placeholder = "Message Session...", bool clearOnSubmit = true)
    {
        this.submit = submit;
        this.placeholder = placeholder;
        this.clearOnSubmit = clearOnSubmit;
    }

    internal void Submit()
    {
        if (!CanSubmit)
            return;
        submit(text.Trim());
        if (!clearOnSubmit)
            return;
        text = "";
        caret = firstCharacter = 0;
        pendingHighSurrogate = null;
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
        if (!Selected)
            return;
        value = string.Concat(value.Select(character => char.IsControl(character) ? ' ' : character));
        int length = Math.Min(value.Length, TextLimit - text.Length);
        if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        text = text.Insert(caret, value[..length]);
        caret += length;
    }

    public void RecieveCommandInput(char command)
    {
        if (!Selected)
            return;
        if (command == '\r')
            Submit();
        else if (command == '\b' && caret > 0)
        {
            int previous = Previous(caret);
            text = text.Remove(previous, caret - previous);
            caret = previous;
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
            case Keys.Home: caret = 0; break;
            case Keys.End: caret = text.Length; break;
            case Keys.Delete when caret < text.Length:
                text = text.Remove(caret, Next(caret) - caret);
                break;
        }
    }

    private int Previous(int index) => index <= 0 ? 0
        : index > 1 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;
    private int Next(int index) => index >= text.Length ? text.Length
        : index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]) ? index + 2 : index + 1;

    internal void Draw(SpriteBatch batch, Rectangle bounds, ModConfig config)
    {
        if (bounds.Width < 16 || bounds.Height < UiText.LineHeight(config.FontScale))
            return;
        batch.Draw(Game1.staminaRect, bounds, Color.Black * (config.Opacity * 0.2f));
        UiStyle.DrawBorder(batch, bounds, config.Opacity);
        if (Selected)
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), UiStyle.Primary * config.Opacity);
        int width = bounds.Width - 14;
        firstCharacter = Math.Min(firstCharacter, caret);
        while (firstCharacter < caret && Game1.smallFont.MeasureString(text[firstCharacter..caret]).X * config.FontScale > width - 2)
            firstCharacter = Next(firstCharacter);
        // Reveal earlier text again after deletion or moving back through the draft.
        while (firstCharacter > 0 && Game1.smallFont.MeasureString(text[Previous(firstCharacter)..caret]).X * config.FontScale <= width - 2)
            firstCharacter = Previous(firstCharacter);
        int end = firstCharacter;
        while (end < text.Length && Game1.smallFont.MeasureString(text[firstCharacter..Next(end)]).X * config.FontScale <= width)
            end = Next(end);
        string visible = text[firstCharacter..end];
        float y = bounds.Y + (bounds.Height - Game1.smallFont.MeasureString("A").Y * config.FontScale) / 2;
        UiText.Draw(batch, text.Length == 0 ? UiText.Fit(placeholder, width, config.FontScale) : visible,
            new Vector2(bounds.X + 6, y), text.Length == 0 ? UiStyle.Muted : UiStyle.Primary, config);
        if (Selected && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000 < 500)
        {
            int x = bounds.X + 6 + (int)MathF.Round(Game1.smallFont.MeasureString(text[firstCharacter..caret]).X * config.FontScale);
            batch.Draw(Game1.staminaRect, new Rectangle(x, (int)MathF.Round(y), 1, UiText.LineHeight(config.FontScale) - 2), UiStyle.Primary * config.Opacity);
        }
    }
}

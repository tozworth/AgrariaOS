using System.Globalization;
using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.UI;

// A native modal owns input and menu lifetime, with the same compact desktop styling.
internal sealed class NumberEntryMenu : IClickableMenu
{
    private const int Padding = 12;
    private readonly ModConfig config;
    private readonly string title;
    private readonly Func<string> prompt;
    private readonly Func<int, string?> submit;
    private readonly Func<bool> isValid;
    private readonly string confirmLabel;
    private readonly TextComposer composer;
    private readonly KeyboardDispatcher dispatcher;
    private Rectangle bounds;
    private Rectangle field;
    private Rectangle confirm;
    private Rectangle cancel;
    private string? error;

    internal NumberEntryMenu(ModConfig config, string title, Func<string> prompt,
        Func<int, string?> submit, Func<bool> isValid, string confirmLabel = "Split") : base(0, 0, 0, 0)
    {
        this.config = config;
        this.title = title;
        this.prompt = prompt;
        this.submit = submit;
        this.isValid = isValid;
        this.confirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "Split" : confirmLabel;
        composer = new TextComposer(Submit, "Quantity...", clearOnSubmit: false);
        dispatcher = Game1.keyboardDispatcher;
        dispatcher.Subscriber = composer;
        Layout();
    }

    private void Layout()
    {
        int line = UiText.LineHeight(config.FontScale);
        int width = Math.Min(Game1.uiViewport.Width, (int)MathF.Ceiling(380 * config.FontScale));
        int height = Math.Min(Game1.uiViewport.Height, line * 5 + Padding * 5 + 8);
        bounds = new Rectangle((Game1.uiViewport.Width - width) / 2,
            (Game1.uiViewport.Height - height) / 2, width, height);
        xPositionOnScreen = bounds.X;
        yPositionOnScreen = bounds.Y;
        this.width = width;
        this.height = height;
        field = new Rectangle(bounds.X + Padding, bounds.Y + Padding * 2 + line * 2,
            Math.Max(0, width - Padding * 2), line + 8);
        int buttonWidth = Math.Max(0, (width - Padding * 3) / 2);
        confirm = new Rectangle(field.X, bounds.Bottom - Padding - line - 8, buttonWidth, line + 8);
        cancel = new Rectangle(confirm.Right + Padding, confirm.Y, buttonWidth, confirm.Height);
    }

    private void Submit(string text)
    {
        if (!isValid())
        {
            exitThisMenu();
            return;
        }
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int amount))
        {
            error = "Enter a whole number.";
            return;
        }
        error = submit(amount);
        if (error == null)
        {
            Game1.playSound("smallSelect");
            exitThisMenu(playSound: false);
        }
    }

    public override void update(GameTime time)
    {
        if (!config.Enabled || !isValid() || dispatcher != Game1.keyboardDispatcher)
            exitThisMenu();
        else if (dispatcher.Subscriber == null)
            dispatcher.Subscriber = composer;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (cancel.Contains(x, y) || !bounds.Contains(x, y))
            exitThisMenu();
        else if (confirm.Contains(x, y))
            composer.Submit();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
            exitThisMenu();
        else
            composer.Navigate(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
            exitThisMenu();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => exitThisMenu();
    protected override void cleanupBeforeExit()
    {
        if (dispatcher.Subscriber == composer)
            dispatcher.Subscriber = null;
        composer.Selected = false;
    }
    public override void emergencyShutDown() => cleanupBeforeExit();

    public override void draw(SpriteBatch batch)
    {
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        batch.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.45f);
        batch.Draw(Game1.staminaRect, bounds, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, bounds, config.Opacity);
        DrawLine(batch, title, bounds.Y + Padding, UiStyle.Primary);
        DrawLine(batch, prompt(), bounds.Y + Padding + UiText.LineHeight(config.FontScale), UiStyle.Muted);
        composer.Draw(batch, field, config);
        DrawLine(batch, error ?? "", field.Bottom + Padding / 2, new Color(241, 155, 130));
        DrawButton(batch, confirmLabel, confirm, cursor, composer.CanSubmit);
        DrawButton(batch, "Cancel", cancel, cursor, true);
        drawMouse(batch);
    }

    private void DrawLine(SpriteBatch batch, string text, int y, Color color)
        => UiText.Draw(batch, UiText.Fit(text, Math.Max(0, bounds.Width - Padding * 2 - 1), config.FontScale),
            new Vector2(bounds.X + Padding, y), color, config);

    private void DrawButton(SpriteBatch batch, string text, Rectangle button, Point cursor, bool enabled)
    {
        batch.Draw(Game1.staminaRect, button, UiStyle.Border * (config.Opacity * (enabled && button.Contains(cursor) ? 1f : 0.45f)));
        UiStyle.DrawBorder(batch, button, config.Opacity);
        Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
        UiText.Draw(batch, text, new Vector2(button.Center.X - size.X / 2, button.Center.Y - size.Y / 2),
            enabled ? UiStyle.Primary : UiStyle.Muted, config);
    }
}

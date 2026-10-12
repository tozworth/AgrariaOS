using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

partial class OptionsWindow
{
    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        drawnRows.Clear();
        scrollTrack = scrollThumb = rowsBounds = paneBounds = treeBounds = Rectangle.Empty;
        rowHeight = Math.Max(30, UiText.LineHeight(config.FontScale) + 10);
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        int treeWidth = Math.Min(GetTreeWidth(config),
            Math.Max(contentBounds.Width / 3, contentBounds.Width - GetMinimumPaneWidth(config)));
        treeBounds = new Rectangle(contentBounds.X, contentBounds.Y, treeWidth, contentBounds.Height);
        tree.Draw(batch, treeBounds, rowHeight, config, cursor, "");
        batch.Draw(Game1.staminaRect, new Rectangle(treeBounds.Right + 1, contentBounds.Y, 1, contentBounds.Height),
            UiStyle.Border * config.Opacity);
        paneBounds = new Rectangle(treeBounds.Right + 4, contentBounds.Y,
            Math.Max(0, contentBounds.Width - treeWidth - 4), contentBounds.Height);
        Rectangle heading = new(paneBounds.X + Padding, paneBounds.Y, Math.Max(0, paneBounds.Width - Padding * 2), rowHeight);
        UiText.DrawCell(batch, SelectedCategory.Label, heading, UiStyle.Primary, config);
        batch.Draw(Game1.staminaRect, new Rectangle(heading.X, heading.Bottom, heading.Width, 1), UiStyle.Border * config.Opacity);
        Rectangle footer = new(heading.X, paneBounds.Bottom - rowHeight, heading.Width, rowHeight);
        int availableHeight = Math.Max(0, footer.Y - heading.Bottom - Padding);
        visibleRows = availableHeight / rowHeight;
        firstRow = Math.Clamp(firstRow, 0, MaxFirstRow);
        rowsBounds = new Rectangle(heading.X, heading.Bottom + Padding,
            Math.Max(0, heading.Width - 10), visibleRows * rowHeight);
        if (visibleRows == 0 || rowsBounds.Width < 80)
        {
            UiText.DrawCell(batch, "Not enough space.", paneBounds, UiStyle.Muted, config);
            return;
        }
        List<OptionsElement> options = SelectedCategory.Options;
        for (int i = firstRow; i < Math.Min(options.Count, firstRow + visibleRows); i++)
        {
            Rectangle row = new(rowsBounds.X, rowsBounds.Y + (i - firstRow) * rowHeight, rowsBounds.Width, rowHeight);
            if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));
            DrawOption(batch, options[i], row, config, cursor);
        }
        if (options.Count == 0)
            UiText.DrawCell(batch, "No options available.", rowsBounds, UiStyle.Muted, config);
        string help = binding != null && capturingInput ? status
            : drawnRows.FirstOrDefault(row => row.Bounds.Contains(cursor))?.Option.label ?? "";
        UiText.DrawCell(batch, help, footer, UiStyle.Muted, config);
        scrollTrack = new Rectangle(rowsBounds.Right + 3, rowsBounds.Y, 6, rowsBounds.Height);
        if (MaxFirstRow > 0)
        {
            int height = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * visibleRows / options.Count));
            int y = scrollTrack.Y + (int)Math.Round((scrollTrack.Height - height) * firstRow / (double)MaxFirstRow);
            scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, height);
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }
        if (dropdown.IsOpen && dropdownOption != null)
        {
            OptionRow? anchor = drawnRows.FirstOrDefault(row => ReferenceEquals(row.Option, dropdownOption));
            if (anchor == null) { dropdown.Close(); ReleaseKeyboardFocus(); }
            else dropdown.Draw(batch, anchor.Control, rowsBounds, rowHeight, config, cursor);
        }
    }

    private void DrawOption(SpriteBatch batch, OptionsElement option, Rectangle row, ModConfig config, Point cursor)
    {
        // These values can also change through native HUD controls or another mod.
        switch (option)
        {
            case OptionsCheckbox checkboxValue: Game1.options.setCheckBoxToProperValue(checkboxValue); break;
            case OptionsSlider sliderValue when sliderDrag == null: Game1.options.setSliderToProperValue(sliderValue); break;
            case OptionsPlusMinus stepperValue: Game1.options.setPlusMinusToProperValue(stepperValue); break;
        }
        bool enabled = !option.greyedOut;
        Color color = enabled ? UiStyle.Primary : UiStyle.Muted;
        int controlWidth = Math.Min(row.Width / 2, Math.Max(140, (int)(180 * config.FontScale)));
        if (option is OptionsSlider)
            controlWidth = Math.Min(row.Width, Math.Max(controlWidth, GetSliderWidth(config)));
        Rectangle control = new(row.Right - controlWidth, row.Y + 3, controlWidth, row.Height - 6);
        Rectangle label = new(row.X, row.Y, Math.Max(0, row.Width - controlWidth - Padding), row.Height);
        Rectangle minus = Rectangle.Empty, plus = Rectangle.Empty, action = Rectangle.Empty;
        if (option is OptionsCheckbox checkbox)
        {
            control = new Rectangle(row.X, row.Y, 28, row.Height);
            label = new Rectangle(control.Right, row.Y, row.Width - control.Width, row.Height);
            FormControls.Checkbox(batch, control, checkbox.isChecked, config, cursor, enabled);
        }
        else if (option is OptionsSlider slider)
        {
            int valueWidth = GetSliderValueWidth(config);
            UiText.DrawCell(batch, $"{slider.value}%", new Rectangle(control.Right - valueWidth, row.Y, valueWidth, row.Height),
                color, config, rightAligned: true);
            control.Width -= valueWidth;
            FormControls.Slider(batch, control, slider.value, config, cursor, enabled);
        }
        else if (option is OptionsDropDown drop)
            FormControls.Dropdown(batch, control, ValueAt(drop.dropDownDisplayOptions, drop.selectedOption), config, cursor,
                enabled, ReferenceEquals(dropdownOption, option) && dropdown.IsOpen);
        else if (option is OptionsPlusMinus stepper)
        {
            if (option is OptionsPlusMinusButton)
            {
                string captureLabel = ScreenshotLabel;
                action = new Rectangle(label.X, control.Y,
                    Math.Min(UiText.CellWidth(captureLabel, config.FontScale), label.Width), control.Height);
                FormControls.Button(batch, action, captureLabel, config, cursor, enabled && !Game1.game1.ScreenshotBusy);
                label = Rectangle.Empty;
            }
            int buttonWidth = Math.Min(30, control.Width / 4);
            minus = new Rectangle(control.X, control.Y, buttonWidth, control.Height);
            plus = new Rectangle(control.Right - buttonWidth, control.Y, buttonWidth, control.Height);
            FormControls.Button(batch, minus, "-", config, cursor, enabled && stepper.selected > 0);
            FormControls.Button(batch, plus, "+", config, cursor, enabled && stepper.selected < stepper.options.Count - 1);
            UiText.DrawCell(batch, ValueAt(stepper.displayOptions, stepper.selected),
                new Rectangle(minus.Right, control.Y, Math.Max(0, plus.X - minus.Right), control.Height), color, config);
        }
        else if (option is OptionsInputListener listener)
        {
            bool listening = ReferenceEquals(binding, listener) && capturingInput;
            string text = listening ? "Press a key..." : listener.whichOption == -1 ? "Reset"
                : string.Join(", ", listener.buttonNames.AsEnumerable().Reverse());
            FormControls.Button(batch, control, text, config, cursor, enabled, listening);
        }
        else if (option is OptionsButton)
        {
            control = new Rectangle(row.X, control.Y, row.Width, control.Height);
            FormControls.Button(batch, control, option.label, config, cursor, enabled);
            label = Rectangle.Empty;
        }
        UiText.DrawCell(batch, option.label, label, color, config);
        drawnRows.Add(new OptionRow(option, row, control, minus, plus, action));
    }

    private static string ValueAt(IReadOnlyList<string> values, int index)
        => index >= 0 && index < values.Count ? values[index] : "";

    private static string ScreenshotLabel
        => Game1.content.LoadString("Strings\\UI:OptionsPage_ScreenshotHeader").TrimEnd(':');

    private int GetTreeWidth(ModConfig config)
        => Math.Max(125, tree.GetPreferredWidth(config.FontScale));

    private int GetSliderValueWidth(ModConfig config)
    {
        // Reserve a stable column for every percentage, including proportional digits.
        if (sliderCaptionSize.Font != Game1.smallFont || sliderCaptionSize.Scale != config.FontScale)
            sliderCaptionSize = (Game1.smallFont, config.FontScale,
                Enumerable.Range(0, 101).Max(value => UiText.CellWidth($"{value}%", config.FontScale)));
        return sliderCaptionSize.Width;
    }

    private int GetSliderWidth(ModConfig config)
        => Math.Max(72, (int)(72 * config.FontScale)) + GetSliderValueWidth(config);

    private int GetMinimumPaneWidth(ModConfig config)
    {
        int rowWidth = Math.Max(160, GetSliderWidth(config) + Padding + (int)(80 * config.FontScale));
        foreach (OptionsElement option in vanilla.options)
        {
            if (option is OptionsPlusMinusButton)
                rowWidth = Math.Max(rowWidth, UiText.CellWidth(ScreenshotLabel, config.FontScale)
                    + Padding + Math.Max(140, (int)(180 * config.FontScale)));
            else if (option is OptionsButton)
                rowWidth = Math.Max(rowWidth, UiText.CellWidth(option.label, config.FontScale));
        }
        // Divider, heading padding and scrollbar; OsWindow supplies the outer border.
        return 4 + Padding * 2 + 10 + rowWidth;
    }
}

using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

partial class OptionsWindow
{
    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (dropdown.ReceiveClick(cursor, leftClick))
        {
            ReleaseKeyboardFocus();
            dropdownOption = null;
            return;
        }
        binding = null;
        ReleaseKeyboardFocus();
        status = "";
        if (treeBounds.Contains(cursor))
        {
            string? previous = tree.SelectedId;
            tree.ReceiveClick(cursor, leftClick);
            if (previous != tree.SelectedId)
            {
                firstRow = 0;
                drawnRows.Clear();
                RefreshOptions();
            }
            return;
        }
        if (!leftClick) return;
        if (!scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            DragContent(cursor);
            return;
        }
        OptionRow? row = drawnRows.FirstOrDefault(row => row.Bounds.Contains(cursor));
        if (row == null || row.Option.greyedOut) return;
        switch (row.Option)
        {
            case OptionsCheckbox checkbox:
                checkbox.receiveLeftClick(checkbox.bounds.Center.X, checkbox.bounds.Center.Y);
                Changed();
                break;
            case OptionsSlider when row.Control.Contains(cursor):
                sliderDrag = row;
                ChangeSlider(row, cursor);
                break;
            case OptionsDropDown drop when row.Control.Contains(cursor):
                dropdownOption = drop;
                dropdown.Open(drop.dropDownDisplayOptions, drop.selectedOption, index =>
                {
                    if (index < 0 || index >= drop.dropDownOptions.Count) return;
                    drop.selectedOption = index;
                    Game1.options.changeDropDownOption(drop.whichOption, drop.dropDownOptions[index]);
                    Changed();
                });
                capturingInput = dropdown.IsOpen;
                break;
            case OptionsPlusMinusButton capture when row.Action.Contains(cursor):
                if (!Game1.game1.ScreenshotBusy)
                    // The native screenshot button's bounds begin at bounds.Left, y=4.
                    InvokeAction(capture, new Point(capture.bounds.Left + 1, 5));
                break;
            case OptionsPlusMinus stepper:
                int direction = row.Minus.Contains(cursor) ? -1 : row.Plus.Contains(cursor) ? 1 : 0;
                int selection = Math.Clamp(stepper.selected + direction, 0, Math.Max(0, stepper.options.Count - 1));
                if (direction != 0 && selection != stepper.selected)
                {
                    stepper.selected = selection;
                    Game1.options.changeDropDownOption(stepper.whichOption, stepper.options[selection]);
                    Game1.playSound("drumkit6");
                    Changed();
                }
                break;
            case OptionsInputListener listener when row.Control.Contains(cursor):
                if (listener.whichOption == -1)
                {
                    Game1.options.setControlsToDefault();
                    Game1.options.optionsDirty = true;
                    Changed();
                }
                else
                {
                    binding = listener;
                    status = Game1.content.LoadString("Strings\\StringsFromCSFiles:OptionsElement.cs.11225");
                    capturingInput = true;
                }
                break;
            case OptionsButton button when row.Control.Contains(cursor):
                InvokeAction(button, button.bounds.Center);
                break;
        }
    }

    private void Changed()
    {
        RefreshOptions();
        SaveOptions();
    }

    private void InvokeAction(OptionsElement option, Point nativeCursor)
    {
        // This action closes its OptionsPage before asking for the co-op menu;
        // ShowLocalCoopJoinMenu requires activeClickableMenu to be null.
        if (option is OptionsButton && option.label == Game1.content.LoadString("Strings\\UI:StartLocalMulti"))
        {
            option.receiveLeftClick(nativeCursor.X, nativeCursor.Y);
            return;
        }
        // OptionsPage's multiplayer callbacks capture activeClickableMenu. Supply a
        // real return target while preserving their vanilla dialog/action lifecycle.
        Game1.activeClickableMenu = actionHost;
        try { option.receiveLeftClick(nativeCursor.X, nativeCursor.Y); }
        finally
        {
            if (ReferenceEquals(Game1.activeClickableMenu, actionHost))
                Game1.activeClickableMenu = null;
        }
    }

    private static void ChangeSlider(OptionRow row, Point cursor)
    {
        if (row.Option is not OptionsSlider slider || slider.greyedOut) return;
        int value = FormControls.SliderValue(row.Control, cursor);
        if (value == slider.value) return;
        slider.value = value;
        Game1.options.changeSliderOption(slider.whichOption, value);
    }

    internal override void DragContent(Point cursor)
    {
        tree.Drag(cursor);
        if (sliderDrag != null) ChangeSlider(sliderDrag, cursor);
        if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (cursor.Y - scrollDragOffset.Value - scrollTrack.Y)
                / (double)(scrollTrack.Height - scrollThumb.Height);
            firstRow = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstRow);
        }
    }

    internal override void EndContentDrag()
    {
        tree.EndDrag();
        scrollDragOffset = null;
        if (sliderDrag != null)
        {
            sliderDrag = null;
            SaveOptions();
        }
    }

    internal override void ReceiveScroll(int delta)
    {
        if (delta == 0) return;
        if (dropdown.IsOpen) { dropdown.ReceiveScroll(delta); return; }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (treeBounds.Contains(cursor)) tree.ReceiveScroll(delta);
        else if (paneBounds.Contains(cursor))
        {
            binding = null;
            ReleaseKeyboardFocus();
            firstRow = (int)Math.Clamp((long)firstRow - Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120) * 3,
                0, MaxFirstRow);
        }
    }

    internal override bool ReceiveShortcut(SButton button)
    {
        if (!capturingInput || !button.TryGetKeyboard(out Keys key)) return false;
        if (dropdown.IsOpen)
        {
            dropdown.ReceiveKey(key);
            capturingInput = dropdown.IsOpen;
            return true;
        }
        if (binding == null) return false;
        if (key == Keys.Escape)
        {
            binding = null;
            ReleaseKeyboardFocus();
        }
        else if (!Game1.options.isKeyInUse(key)
            || new InputButton(key).ToString() == binding.buttonNames.FirstOrDefault())
        {
            Game1.options.changeInputListenerValue(binding.whichOption, key);
            Game1.playSound("coin");
            binding = null;
            ReleaseKeyboardFocus();
            Changed();
        }
        else status = Game1.content.LoadString("Strings\\StringsFromCSFiles:OptionsElement.cs.11228");
        return true;
    }
}

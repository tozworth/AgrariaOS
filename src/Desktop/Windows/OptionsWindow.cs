using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

// Vanilla supplies the localized categories, available options and setting actions.
// This window owns presentation and input; it never draws the vanilla OptionsPage.
internal sealed partial class OptionsWindow : OsWindow
{
    internal const string WindowId = "Options";
    private const int Padding = 8;
    private readonly TreeView tree = new();
    private readonly DropdownList dropdown = new();
    private readonly List<Category> categories = new();
    private readonly List<OptionRow> drawnRows = new();
    private readonly InputCapture input = new();
    private bool capturingInput;
    private OptionsPage vanilla = null!;
    private (int Mode, bool Multiplayer, bool Joinable, bool Server, LocalizedContentManager.LanguageCode Language) context;
    private bool hadNativeMenu;
    private readonly OptionsActionHost actionHost = new();
    private Rectangle treeBounds;
    private Rectangle paneBounds;
    private Rectangle rowsBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int rowHeight;
    private (SpriteFont? Font, float Scale, int Width) sliderCaptionSize;
    private int firstRow;
    private int visibleRows;
    private int? scrollDragOffset;
    private OptionRow? sliderDrag;
    private OptionsElement? dropdownOption;
    private OptionsInputListener? binding;
    private string status = "";
    private Category SelectedCategory => categories.First(category => category.Id == tree.SelectedId);
    private int MaxFirstRow => Math.Max(0, SelectedCategory.Options.Count - visibleRows);

    internal OptionsWindow()
        => RebuildCategories();

    private static (int, bool, bool, bool, LocalizedContentManager.LanguageCode) GetContext()
        => (Game1.multiplayerMode, Game1.IsMultiplayer, Game1.game1.IsLocalCoopJoinable(),
            Game1.server != null, Game1.content.GetCurrentLanguage());

    private void RebuildCategories()
    {
        string? previousLabel = tree.SelectedNode?.Label;
        context = GetContext();
        categories.Clear();
        drawnRows.Clear();
        dropdown.Close();
        dropdownOption = null;
        ReleaseKeyboardFocus();
        binding = null;
        firstRow = 0;
        // Use vanilla's full-size constructor even when the desktop is resized. Its
        // element geometry is retained only for forwarding discrete button actions.
        vanilla = new OptionsPage(0, 0, 800, 600);
        Category? current = null;
        foreach (OptionsElement option in vanilla.options)
        {
            if (option.GetType() == typeof(OptionsElement))
            {
                current = new Category($"category:{categories.Count}", option.label.TrimEnd(':'));
                categories.Add(current);
            }
            else
                current?.Options.Add(option);
        }
        tree.SetRoots(categories.Select(category => new TreeNode(category.Id, category.Label)));
        tree.Select((categories.FirstOrDefault(category => category.Label == previousLabel) ?? categories[0]).Id);
    }

    internal override string Id => WindowId;
    internal override string Title => Game1.content.LoadString("Strings\\UI:GameMenu_Options");
    internal override int Icon => UiStyle.OptionsIcon;
    internal override Point DefaultSize => new(800, 520);
    protected override Point GetMinimumSize(ModConfig config)
        => new(Math.Max(460, GetTreeWidth(config) + GetMinimumPaneWidth(config) + 2), TitleHeight + 160);
    internal override IKeyboardSubscriber? KeyboardSubscriber => input;
    // Vanilla's UpdateOther clears the dispatcher (and Selected) outside menus.
    // Keep window intent separate so the manager can restore this capture each tick.
    internal override bool WantsKeyboardFocus => capturingInput;
    internal override void ReleaseKeyboardFocus()
    {
        capturingInput = false;
        input.Selected = false;
    }

    private sealed record Category(string Id, string Label)
    {
        internal List<OptionsElement> Options { get; } = new();
    }

    private sealed record OptionRow(OptionsElement Option, Rectangle Bounds, Rectangle Control,
        Rectangle Minus, Rectangle Plus, Rectangle Action);

    // Capture key bindings/dropdown navigation through the desktop's existing
    // subscriber ownership and suppression, without stealing another menu's input.
    private sealed class InputCapture : IKeyboardSubscriber
    {
        public bool Selected { get; set; }
        public void RecieveTextInput(char inputChar) { }
        public void RecieveTextInput(string text) { }
        public void RecieveCommandInput(char command) { }
        public void RecieveSpecialInput(Keys key) { }
    }

    private sealed class OptionsActionHost : IClickableMenu { }

    internal override void Update()
    {
        // Multiplayer dialogs return to the menu captured by OptionsPage. Returning
        // to this invisible action host restores the desktop instead of a vanilla tab.
        if (ReferenceEquals(Game1.activeClickableMenu, actionHost))
            Game1.activeClickableMenu = null;
        bool nativeMenu = Game1.activeClickableMenu != null;
        // Reopening vanilla options or starting co-op can change available rows and
        // values while this desktop window survives in the background.
        if (!nativeMenu && (hadNativeMenu || Game1.ticks % 60 == 0 && context != GetContext()))
            RebuildCategories();
        hadNativeMenu = nativeMenu;
        if (!capturingInput)
        {
            binding = null;
            dropdown.Close();
            dropdownOption = null;
        }
    }

    internal override void Close()
    {
        ReleaseKeyboardFocus();
        dropdown.Close();
        binding = null;
        SaveOptions();
    }

    private static void SaveOptions()
    {
        if (Game1.options.optionsDirty)
            Game1.options.SaveDefaultOptions();
    }

    private void RefreshOptions()
    {
        foreach (OptionsElement option in vanilla.options)
        {
            switch (option)
            {
                case OptionsCheckbox checkbox: Game1.options.setCheckBoxToProperValue(checkbox); break;
                case OptionsSlider slider: Game1.options.setSliderToProperValue(slider); break;
                case OptionsDropDown drop:
                    // Vanilla appends choices rather than replacing them.
                    drop.dropDownOptions.Clear();
                    drop.dropDownDisplayOptions.Clear();
                    Game1.options.setDropDownToProperValue(drop);
                    break;
                case OptionsPlusMinus plus: Game1.options.setPlusMinusToProperValue(plus); break;
                case OptionsInputListener listener when listener.whichOption != -1:
                    Game1.options.setInputListenerToProperValue(listener);
                    break;
            }
        }
    }
}

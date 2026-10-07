using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Hud;
using AgrariaOS.Notifications;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

public sealed partial class ModEntry : Mod
{
    private static ModEntry Instance = null!;
    private readonly PerScreen<bool> osHudActive = new(() => true);
    private readonly PerScreen<StatusReadoutRenderer> statusReadoutRenderer = new(() => new StatusReadoutRenderer());
    private readonly PerScreen<ToolbarRenderer> toolbarRenderer = new(() => new ToolbarRenderer());
    private readonly PerScreen<NotificationHistory> notificationHistory = new(() => new NotificationHistory());
    private readonly PerScreen<ChatSession> chatHistory = new(() => new ChatSession());
    private readonly PerScreen<bool> dialogueRoutingFailed = new(() => false);
    private readonly PerScreen<NotificationBubbleRenderer> notificationBubbleRenderer = new(() => new NotificationBubbleRenderer());
    private readonly PerScreen<Pelicom> pelicom = new(() => new Pelicom(Instance.config,
        () => Instance.UsePelicom, Instance.OnPelicomDrawFailed, () => Instance.windowManager.Value.OpenMail(),
        () => Instance.windowManager.Value.IsMinimized(MailWindow.WindowId),
        () => Instance.windowManager.Value.OpenInventory(),
        () => Instance.windowManager.Value.IsMinimized(InventoryWindow.WindowId),
        () => Instance.windowManager.Value.OpenNotifications(Instance.notificationHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(NotificationsWindow.WindowId),
        () => Instance.notificationHistory.Value.UnreadCount,
        () => Instance.windowManager.Value.OpenChat(Instance.chatHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(ChatWindow.WindowId),
        (cursor, items) => Instance.windowManager.Value.ShowContextMenu(cursor, items)));
    private readonly PerScreen<WindowManager> windowManager = new(() => new WindowManager(Instance.config,
        () => Instance.pelicom.Value.IsInteractive, Instance.OnWindowDrawFailed, () => Instance.Helper.WriteConfig(Instance.config)));
    private ModConfig config = null!;
    private bool patchInstalled;
    private bool toolbarPatchInstalled;
    private bool vitalsPatchInstalled;
    private bool notificationPatchInstalled;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        config = helper.ReadConfig<ModConfig>();
        config.Normalize();
        helper.WriteConfig(config);

        InstallHudPatches();
        InstallWorldInteractionPatches();

        helper.Events.GameLoop.DayStarted += (_, _) =>
        {
            // SMAPI also raises DayStarted after loading a save; use the game's normal toast path.
            if (config.Enabled && Context.IsWorldReady)
                Game1.showGlobalMessage("Welcome to Agraria OS test-0184. Enjoy your stay.");
        };
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.UpdateTicking += (_, _) => windowManager.Value.SuppressKeyboardInput(helper.Input);
        helper.Events.Input.MouseWheelScrolled += (_, e) =>
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            if (UsePelicom && windowManager.Value.ReceiveScroll(cursor, e.Delta))
                helper.Input.SuppressScrollWheel();
        };
        helper.Events.Display.RenderedHud += (_, e) => windowManager.Value.Draw(e.SpriteBatch);
        helper.Events.Display.RenderingActiveMenu += (_, e) =>
        {
            // The opaque menu background skips the HUD render pass entirely.
            if (Game1.activeClickableMenu is GameMenu && Game1.options.showMenuBackground)
                pelicom.Value.draw(e.SpriteBatch);
        };
        helper.Events.Display.WindowResized += (_, _) => windowManager.Value.InvalidateLayout();
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            // Vanilla recreates its HUD menus on load. Keep one Pelicom per local screen.
            if (patchInstalled && config.Enabled && Context.IsWorldReady && !Game1.onScreenMenus.Contains(pelicom.Value))
                Game1.onScreenMenus.Add(pelicom.Value);
            RouteDialogue();
            windowManager.Value.Update(helper.Input);
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            foreach (var screen in windowManager.GetActiveValues())
            {
                screen.Value.ClearKeyboardFocus();
                screen.Value.EndDrag();
            }
            osHudActive.ResetAllScreens();
            statusReadoutRenderer.ResetAllScreens();
            toolbarRenderer.ResetAllScreens();
            notificationHistory.ResetAllScreens();
            chatHistory.ResetAllScreens();
            dialogueRoutingFailed.ResetAllScreens();
            notificationBubbleRenderer.ResetAllScreens();
            pelicom.ResetAllScreens();
            windowManager.ResetAllScreens();
            rightClickAction.ResetAllScreens();
            telephoneTarget.ResetAllScreens();
            worldInteractionsFailed.ResetAllScreens();
        };
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (UsePelicom && !e.IsSuppressed() && e.Button is SButton.MouseLeft or SButton.MouseRight)
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            bool leftClick = e.Button == SButton.MouseLeft;
            if (windowManager.Value.ReceiveClick(cursor, leftClick))
            {
                Helper.Input.Suppress(e.Button);
                return;
            }
            if (!Game1.IsChatting && pelicom.Value.isWithinBounds(cursor.X, cursor.Y))
            {
                if (leftClick)
                    pelicom.Value.receiveLeftClick(cursor.X, cursor.Y);
                else
                    pelicom.Value.receiveRightClick(cursor.X, cursor.Y);
                Helper.Input.Suppress(e.Button);
                return;
            }
        }

        if (UsePelicom && e.Button != config.ToggleKey && !e.IsSuppressed()
            && windowManager.Value.ReceiveKeyboardButton(e.Button, Helper.Input))
        {
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (!patchInstalled || !config.Enabled || !Context.IsWorldReady || !Context.IsPlayerFree || e.Button != config.ToggleKey)
            return;

        osHudActive.Value = !osHudActive.Value;
        windowManager.Value.InvalidateLayout();
        Helper.Input.Suppress(e.Button);
        // Restore click targets before the next input update, including while the HUD is hidden.
        foreach (Toolbar toolbar in Game1.onScreenMenus.OfType<Toolbar>())
        {
            if (!UseCompactToolbar)
                toolbarRenderer.Value.RestoreLayout(toolbar);
        }
        Monitor.Log(osHudActive.Value ? "Agraria OS HUD enabled." : "Vanilla HUD enabled.", LogLevel.Info);
    }

    private bool UseCompactToolbar => toolbarPatchInstalled && config.Enabled && osHudActive.Value && Context.IsWorldReady;

    private bool UsePelicom => patchInstalled && config.Enabled && osHudActive.Value && Context.IsWorldReady;

    private void OnPelicomDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        Monitor.Log($"Pelicom failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private void OnWindowDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        Monitor.Log($"Window drawing failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    // Suppress vanilla bars even while a menu temporarily hides the compact toolbar.
    private bool UseTextVitals => vitalsPatchInstalled && UseCompactToolbar
        && Game1.onScreenMenus.OfType<Toolbar>().Any();
}

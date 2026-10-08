using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Hud;
using AgrariaOS.Notifications;
using AgrariaOS.UI;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
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
    private readonly PerScreen<ClickToMove> clickToMove = new(() => new ClickToMove());
    private readonly PerScreen<bool> clickToMoveFailed = new(() => false);
    private readonly PerScreen<StatusReadoutRenderer> statusReadoutRenderer = new(() => new StatusReadoutRenderer());
    private readonly PerScreen<ToolbarRenderer> toolbarRenderer = new(() => new ToolbarRenderer());
    private readonly PerScreen<NotificationHistory> notificationHistory = new(() => new NotificationHistory());
    private readonly MiningLedger miningLedger = new();
    private readonly PerScreen<ChatSession> chatHistory = new(() => new ChatSession());
    private readonly PerScreen<bool> dialogueRoutingFailed = new(() => false);
    private readonly PerScreen<NotificationBubbleRenderer> notificationBubbleRenderer = new(() => new NotificationBubbleRenderer());
    private readonly PerScreen<Pelicom> pelicom = new(() => new Pelicom(Instance.config,
        () => Instance.UsePelicom, Instance.OnPelicomDrawFailed, () => Instance.windowManager.Value.OpenMail(),
        () => Instance.windowManager.Value.IsMinimized(MailWindow.WindowId),
        () => Instance.windowManager.Value.OpenInventory(),
        () => Instance.windowManager.Value.IsMinimized(InventoryWindow.WindowId),
        () => Instance.windowManager.Value.OpenLivestock(),
        () => Instance.windowManager.Value.IsMinimized(LivestockWindow.WindowId),
        () => Instance.windowManager.Value.OpenNotifications(Instance.notificationHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(NotificationsWindow.WindowId),
        () => Instance.notificationHistory.Value.UnreadCount,
        () => Instance.windowManager.Value.OpenChat(Instance.chatHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(ChatWindow.WindowId),
        (cursor, items) => Instance.windowManager.Value.ShowContextMenu(cursor, items),
        () => Instance.windowManager.Value.GetMinimizedWindowItems(),
        () => Instance.windowManager.Value.OpenMiningLedger(Instance.miningLedger),
        () => Instance.windowManager.Value.IsMinimized(MiningLedgerWindow.WindowId),
        () => Instance.windowManager.Value.OpenOverview(),
        () => Instance.windowManager.Value.IsMinimized(OverviewWindow.WindowId)));
    private readonly PerScreen<WindowManager> windowManager = new(() => new WindowManager(Instance.config,
        () => Instance.pelicom.Value.IsInteractive, Instance.OnWindowDrawFailed,
        () => Instance.Helper.WriteConfig(Instance.config), Instance.OnInventoryTransferFailed, Instance.lookupAnything,
        () => Instance.clickToMove.Value));
    private readonly PerScreen<bool> windowsRestored = new(() => false);
    private LookupAnythingIntegration lookupAnything = null!;
    private readonly HardwareCursor hardwareCursor = new();
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
        lookupAnything = new LookupAnythingIntegration(helper, Monitor, ModManifest.UniqueID);
        try { lookupAnything.Prepare(); }
        catch (Exception ex) { lookupAnything.Disable(ex); }
        helper.Events.GameLoop.GameLaunched += (_, _) => InitializeLookupAnything();

        InstallHudPatches();
        InstallDesktopPausePatches();
        InstallWorldInteractionPatches();
        InstallMiningPatches();

        helper.Events.GameLoop.DayStarted += (_, _) =>
        {
            // SMAPI also raises DayStarted after loading a save; use the game's normal toast path.
            if (config.Enabled && Context.IsWorldReady)
                Game1.showGlobalMessage("Welcome to Agraria OS test-0184. Enjoy your stay.");
        };
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.UpdateTicking += (_, _) =>
        {
            windowManager.Value.SuppressKeyboardInput(helper.Input);
            UpdateDesktopPause();
            UpdateClickToMove(beforeUpdate: true);
        };
        helper.Events.Input.MouseWheelScrolled += (_, e) =>
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            if (UsePelicom && windowManager.Value.ReceiveScroll(cursor, e.Delta))
                helper.Input.SuppressScrollWheel();
        };
        helper.Events.Display.RenderedHud += (_, e) => windowManager.Value.Draw(e.SpriteBatch);
        helper.Events.Display.RenderedWorld += (_, e) =>
        {
            DrawHoverHighlight(e.SpriteBatch);
            DrawMoveDestination(e.SpriteBatch);
        };
        helper.Events.Display.RenderingActiveMenu += (_, e) =>
        {
            // The opaque menu background skips the HUD render pass entirely.
            if (Game1.activeClickableMenu is GameMenu && Game1.options.showMenuBackground)
                pelicom.Value.draw(e.SpriteBatch);
        };
        helper.Events.Display.WindowResized += (_, _) =>
        {
            windowManager.Value.InvalidateLayout();
            hardwareCursor.Reset();
        };
        helper.Events.Display.MenuChanged += (_, e) =>
        {
            // Preserve the queue across dialogues/menus: Before/AfterUpdate pause on
            // !canMove and StartNextQueued skips stale legs, so no cancel is needed here.
            hardwareCursor.Reset();
            // Also release the text field if another mod or a title transition replaces it.
            if (e.OldMenu is NumberEntryMenu numberEntry)
                numberEntry.emergencyShutDown();
        };
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            UpdateClickToMove(beforeUpdate: false);
            // Vanilla recreates its HUD menus on load. Keep one Pelicom per local screen.
            if (patchInstalled && config.Enabled && Context.IsWorldReady && !Game1.onScreenMenus.Contains(pelicom.Value))
                Game1.onScreenMenus.Add(pelicom.Value);
            // Restore the farmer's last open windows once per screen session, in the
            // background so the game doesn't start paused. Nothing is forced open.
            if (!windowsRestored.Value && UsePelicom)
            {
                windowsRestored.Value = true;
                windowManager.Value.RestoreSavedWindows(notificationHistory.Value, chatHistory.Value, miningLedger);
            }
            RouteDialogue();
            windowManager.Value.Update(helper.Input);
            UpdateHardwareCursor();
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            foreach (var screen in clickToMove.GetActiveValues())
                screen.Value.Cancel();
            clickToMove.ResetAllScreens();
            clickToMoveFailed.ResetAllScreens();
            hardwareCursor.Reset();
            foreach (var screen in windowManager.GetActiveValues())
            {
                screen.Value.CloseAll();
            }
            osHudActive.ResetAllScreens();
            foreach (var screen in statusReadoutRenderer.GetActiveValues())
                screen.Value.Dispose();
            statusReadoutRenderer.ResetAllScreens();
            toolbarRenderer.ResetAllScreens();
            notificationHistory.ResetAllScreens();
            miningLedger.ClearAll();
            chatHistory.ResetAllScreens();
            dialogueRoutingFailed.ResetAllScreens();
            notificationBubbleRenderer.ResetAllScreens();
            pelicom.ResetAllScreens();
            windowManager.ResetAllScreens();
            windowsRestored.ResetAllScreens();
            rightClickAction.ResetAllScreens();
            interactArrival.ResetAllScreens();
            telephoneTarget.ResetAllScreens();
            worldInteractionsFailed.ResetAllScreens();
        };
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        // New destinations, UI clicks and ordinary controls all release our route first.
        // This only clears a controller we own, never one installed by an event/other mod.
        // Mouse clicks manage the queue themselves (replace vs append); clearing here
        // would drop the queue before an explicit Ctrl+click or auto-queued rock can append.
        if (e.Button is not (SButton.LeftShift or SButton.RightShift or SButton.LeftControl or SButton.RightControl
            or SButton.LeftAlt or SButton.RightAlt or SButton.MouseLeft or SButton.MouseRight))
        {
            // Opening the overview is UI chrome, not a new destination or tool action.
            if (e.Button != config.OverviewKey)
                clickToMove.Value.Cancel();
        }
        // Release the modal before evaluating the usual player-free F8 fallback.
        if (e.Button == config.ToggleKey && Game1.activeClickableMenu is NumberEntryMenu numberEntry)
            numberEntry.exitThisMenu(playSound: false);

        if (UsePelicom && !e.IsSuppressed() && e.Button is SButton.MouseLeft or SButton.MouseRight)
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            bool leftClick = e.Button == SButton.MouseLeft;
            if (windowManager.Value.ReceiveClick(cursor, leftClick, Helper.Input))
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
            if (!Game1.IsChatting && !Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
                windowManager.Value.FocusWorld();
        }

        if (UsePelicom && e.Button != config.ToggleKey && !e.IsSuppressed()
            && windowManager.Value.ReceiveKeyboardButton(e.Button, Helper.Input))
        {
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (e.Button == SButton.MouseLeft && !e.IsSuppressed() && (CanClickToMove || CanAppendToQueue)
            && !Helper.Input.IsDown(SButton.LeftShift) && !Helper.Input.IsDown(SButton.RightShift))
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Keep clock/journal/zoom, toolbar and other mods' HUD menus in vanilla's hands.
            if (!Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
            {
                try
                {
                    Vector2 pixel = e.Cursor.AbsolutePixels;
                    Point tile = new Point((int)MathF.Floor(pixel.X / 64), (int)MathF.Floor(pixel.Y / 64));
                    bool busyBefore = clickToMove.Value.IsBusy;
                    bool moveAllowed = CanClickToMove;
                    bool queued = TryQueueLeftClick(tile);
                    bool started = false;
                    // Append-only mode (mid-swing) must never Start: that would hijack
                    // the swing with a fresh route and wipe the repeat session.
                    if (!queued && moveAllowed)
                        started = clickToMove.Value.Start(tile);
                    Monitor.Log($"LeftClick loc={Game1.currentLocation?.Name} tile={tile} playerTile={Game1.player.TilePoint} busyBefore={busyBefore} moveAllowed={moveAllowed} queued={queued} started={started} {clickToMove.Value.LastDebug}", LogLevel.Trace);
                    if (queued || started)
                        Helper.Input.Suppress(e.Button);
                }
                catch (Exception ex)
                {
                    OnClickToMoveFailed(ex);
                }
                return;
            }
        }

        if (e.Button == SButton.MouseRight && !e.IsSuppressed() && (CanClickToMove || CanAppendToQueue)
            && !Helper.Input.IsDown(SButton.LeftShift) && !Helper.Input.IsDown(SButton.RightShift))
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Same HUD reservation as left-click; in-range clicks fall through to vanilla.
            if (!Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
            {
                Vector2 pixel = e.Cursor.AbsolutePixels;
                if (TryStartRightClickInteract(new Point((int)MathF.Floor(pixel.X / 64), (int)MathF.Floor(pixel.Y / 64))))
                    Helper.Input.Suppress(e.Button);
                // Unactionable far tiles keep vanilla's facing-tile action plus Lookup info.
            }
        }

        if (UsePelicom && !Game1.IsChatting && !e.IsSuppressed() && e.Button == config.OverviewKey)
        {
            windowManager.Value.OpenOverview();
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (!patchInstalled || !config.Enabled || !Context.IsWorldReady || !Context.IsPlayerFree || e.Button != config.ToggleKey)
            return;

        osHudActive.Value = !osHudActive.Value;
        windowManager.Value.InvalidateLayout();
        hardwareCursor.Reset();
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

    private void UpdateHardwareCursor()
    {
        // Stardew disables hardware cursors in split screen. Only the primary screen
        // manages the process-wide cursor so another screen can't overwrite it.
        if (Context.ScreenId != 0)
            return;
        if (!UsePelicom || !Game1.options.hardwareCursor || !Game1.game1.IsActiveNoOverlay)
        {
            hardwareCursor.Reset();
            return;
        }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        hardwareCursor.Update(windowManager.Value.GetResizeCursorEdges(cursor));
    }

    private void OnPelicomDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        Monitor.Log($"Pelicom failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private void OnWindowDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        Monitor.Log($"Window drawing failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private void OnInventoryTransferFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        windowManager.Value.InvalidateLayout();
        Monitor.Log($"Inventory transfer failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    // Suppress vanilla bars even while a menu temporarily hides the compact toolbar.
    private bool UseTextVitals => vitalsPatchInstalled && UseCompactToolbar
        && Game1.onScreenMenus.OfType<Toolbar>().Any();
}

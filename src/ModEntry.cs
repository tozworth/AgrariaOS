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
using AgrariaOS.Wallet;
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
    private readonly PerScreen<MachineCollectDrag> machineCollectDrag = new(() => new MachineCollectDrag());
    private readonly PerScreen<MachineCollectDrag> rockMiningDrag = new(() => new MachineCollectDrag());
    private readonly PerScreen<MachineCollectDrag> animalHarvestDrag = new(() => new MachineCollectDrag());
    private readonly PerScreen<StatusReadoutRenderer> statusReadoutRenderer = new(() => new StatusReadoutRenderer());
    private readonly PerScreen<ToolbarRenderer> toolbarRenderer = new(() => new ToolbarRenderer());
    private readonly PerScreen<NotificationHistory> notificationHistory = new(() => new NotificationHistory());
    private readonly MiningLedger miningLedger = new();
    private readonly WalletLedger walletLedger = new();
    private readonly PerScreen<ChatSession> chatHistory = new(() => new ChatSession());
    private readonly PerScreen<bool> dialogueRoutingFailed = new(() => false);
    private readonly PerScreen<NotificationBubbleRenderer> notificationBubbleRenderer = new(() => new NotificationBubbleRenderer());
    private readonly PerScreen<Pelicom> pelicom = new(() => new Pelicom(Instance.config,
        () => Instance.UsePelicom, Instance.OnPelicomDrawFailed, () => Instance.windowManager.Value.OpenMail(),
        () => Instance.windowManager.Value.IsMinimized(MailWindow.WindowId),
        () => Instance.windowManager.Value.OpenJournal(),
        () => Instance.windowManager.Value.IsMinimized(JournalWindow.WindowId),
        () => { try { return Context.IsWorldReady && Game1.player.hasNewQuestActivity(); } catch { return false; } },
        () => Instance.windowManager.Value.OpenInventory(),
        () => Instance.windowManager.Value.IsMinimized(InventoryWindow.WindowId),
        () => Instance.windowManager.Value.OpenLivestock(),
        () => Instance.windowManager.Value.IsMinimized(LivestockWindow.WindowId),
        () => Instance.windowManager.Value.OpenSocial(),
        () => Instance.windowManager.Value.IsMinimized(SocialWindow.WindowId),
        () => Instance.windowManager.Value.OpenNotifications(Instance.notificationHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(NotificationsWindow.WindowId),
        () => Instance.notificationHistory.Value.UnreadCount,
        () => Instance.windowManager.Value.OpenChat(Instance.chatHistory.Value),
        () => Instance.windowManager.Value.IsMinimized(ChatWindow.WindowId),
        () => Instance.windowManager.Value.OpenNotes(),
        () => Instance.windowManager.Value.IsMinimized(NotesWindow.WindowId),
        (cursor, items) => Instance.windowManager.Value.ShowContextMenu(cursor, items),
        () => Instance.windowManager.Value.OpenMiningLedger(Instance.miningLedger),
        () => Instance.windowManager.Value.IsMinimized(MiningLedgerWindow.WindowId),
        () => Instance.windowManager.Value.OpenBundles(),
        () => Instance.windowManager.Value.IsMinimized(BundlesWindow.WindowId),
        () => BundlesWindow.IsAvailable(),
        () => Instance.windowManager.Value.OpenWallet(Instance.walletLedger),
        () => Instance.windowManager.Value.IsMinimized(WalletWindow.WindowId),
        () => Instance.windowManager.Value.OpenOverview(),
        () => Instance.windowManager.Value.IsMinimized(OverviewWindow.WindowId),
        () => Instance.Helper.WriteConfig(Instance.config)));
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
        InstallWalletPatches();
        InstallMailPatches();
        WalletLedger.Active = walletLedger;

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
            UpdateMachineCollectDrag();
            UpdateRockMiningDrag();
            UpdateAnimalHarvestDrag();
            UpdateClickToMove(beforeUpdate: true);
        };
        helper.Events.Input.MouseWheelScrolled += (_, e) =>
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            if (UsePelicom && windowManager.Value.ReceiveScroll(cursor, e.Delta))
                helper.Input.SuppressScrollWheel();
        };
        helper.Events.Display.RenderedHud += (_, e) => windowManager.Value.Draw(e.SpriteBatch, batch => pelicom.Value.DrawTooltip(batch));
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
            HandleShopMenuChanged(e.OldMenu, e.NewMenu);
        };
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            UpdateClickToMove(beforeUpdate: false);
            walletLedger.Sync();
            SyncMail();
            // Vanilla recreates its HUD menus on load. Keep one Pelicom per local screen.
            if (patchInstalled && config.Enabled && Context.IsWorldReady && !Game1.onScreenMenus.Contains(pelicom.Value))
                Game1.onScreenMenus.Add(pelicom.Value);
            // Restore the farmer's last open windows once per screen session, in the
            // background so the game doesn't start paused. Nothing is forced open.
            if (!windowsRestored.Value && UsePelicom)
            {
                windowsRestored.Value = true;
                windowManager.Value.RestoreSavedWindows(notificationHistory.Value, chatHistory.Value, miningLedger, walletLedger);
            }
            RouteDialogue();
            windowManager.Value.Update(helper.Input);
            try { pelicom.Value.Update(helper.Input); }
            catch { }
            UpdateHardwareCursor();
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            foreach (var screen in clickToMove.GetActiveValues())
                screen.Value.Cancel();
            clickToMove.ResetAllScreens();
            clickToMoveFailed.ResetAllScreens();
            machineCollectDrag.ResetAllScreens();
            rockMiningDrag.ResetAllScreens();
            animalHarvestDrag.ResetAllScreens();
            animalQueueOutline.Clear();
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
            walletLedger.ClearAll();
            mailArchive.Clear();
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








    // Suppress vanilla bars even while a menu temporarily hides the compact toolbar.
}

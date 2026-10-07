using AgrariaOS.Interactions;
using AgrariaOS.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private readonly WorldContextMenuRouter worldContextMenus = new();
    private readonly PerScreen<bool> rightClickAction = new(() => false);
    private readonly PerScreen<Phone?> telephoneTarget = new(() => null);
    private readonly PerScreen<bool> worldInteractionsFailed = new(() => false);
    private bool worldInteractionPatchesInstalled;

    private void InstallWorldInteractionPatches()
    {
        if (!patchInstalled)
            return;
        try
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressActionButton)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(WorldActionPrefix)),
                finalizer: new HarmonyMethod(typeof(ModEntry), nameof(WorldActionFinalizer)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Phone), nameof(Phone.checkForAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(TelephoneActionPrefix)),
                finalizer: new HarmonyMethod(typeof(ModEntry), nameof(TelephoneActionFinalizer)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Chest), nameof(Chest.checkForAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ChestActionPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Chest), nameof(Chest.performObjectDropInAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ChestDropInPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.createQuestionDialogue),
                    new[] { typeof(string), typeof(Response[]), typeof(GameLocation.afterQuestionBehavior), typeof(NPC) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(CallbackQuestionPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(BusStop), nameof(BusStop.checkAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(BusTicketActionPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.ShowPagedResponses)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(TelephoneDirectoryPrefix)));
            worldInteractionPatchesInstalled = true;
            Monitor.Log("World right-click interactions ready (chest inventories, TV, telephone and bus tickets).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't enable world right-click menus; keeping vanilla interactions.\n{ex}", LogLevel.Error);
        }
    }

    private bool CanRouteWorldInteraction => worldInteractionPatchesInstalled && UsePelicom
        && !worldInteractionsFailed.Value && rightClickAction.Value && Context.IsPlayerFree
        && pelicom.Value.IsInteractive;

    // Scope only the game's mouse action dispatch. Let vanilla resolve targets, reach,
    // facing, horses, furniture hitboxes and HUD priority before intercepting a question.
    private static bool WorldActionPrefix(KeyboardState currentKBState, MouseState currentMouseState,
        GamePadState currentPadState, ref bool __result, out bool __state)
    {
        ModEntry mod = Instance;
        __state = mod.rightClickAction.Value;
        bool mouseAction = currentMouseState.RightButton == ButtonState.Pressed
            && !Game1.isOneOfTheseKeysDown(currentKBState, Game1.options.actionButton)
            && !currentPadState.IsButtonDown(Buttons.A);
        mod.rightClickAction.Value = mouseAction && Game1.oldMouseState.RightButton == ButtonState.Released;
        if (mod.UsePelicom && mod.windowManager.Value.IsContextMenuOpen)
        {
            if (mouseAction)
            {
                mod.Helper.Input.Suppress(SButton.MouseRight);
                __result = false;
                return false;
            }
            // Keyboard/controller actions keep the ordinary interaction, even with a popup open.
            mod.windowManager.Value.CloseContextMenu();
        }
        return true;
    }

    private static void WorldActionFinalizer(bool __state) => Instance.rightClickAction.Value = __state;

    private bool CanRouteChest(Chest chest, Farmer who) => CanRouteWorldInteraction
        && who == Game1.player && chest.playerChest.Value && !chest.giftbox.Value
        && Game1.currentLocation.Objects.TryGetValue(chest.TileLocation, out var placed)
        && ReferenceEquals(placed, chest);

    private static bool ChestActionPrefix(Chest __instance, Farmer who, bool justCheckingForActivity,
        ref bool __result)
    {
        ModEntry mod = Instance;
        if (justCheckingForActivity || !mod.CanRouteChest(__instance, who))
            return true;
        try
        {
            // The desktop reads live contents; transfers acquire their own short-lived lock.
            mod.windowManager.Value.OpenChestInventory(__instance);
            mod.Helper.Input.Suppress(SButton.MouseRight);
            __result = true;
            return false;
        }
        catch (Exception ex)
        {
            mod.OnWorldInteractionFailed(ex);
            return true;
        }
    }

    private static bool ChestDropInPrefix(Chest __instance, Farmer who, ref bool __result)
    {
        if (!Instance.CanRouteChest(__instance, who))
            return true;
        // GameLocation.checkAction probes and attempts drop-in before checkForAction when
        // an item is held. Block both so a held replacement chest cannot mutate this view.
        __result = false;
        return false;
    }

    private static bool TelephoneActionPrefix(Phone __instance, Farmer who, bool justCheckingForActivity,
        ref bool __result, out Phone? __state)
    {
        ModEntry mod = Instance;
        __state = mod.telephoneTarget.Value;
        if (!mod.CanRouteWorldInteraction || justCheckingForActivity || who != Game1.player)
            return true;
        mod.telephoneTarget.Value = __instance;
        if (Phone.whichPhoneCall == null || Game1.IsGreenRainingHere())
            return true;
        // The normal incoming-call branch skips the directory; ask the game's directory
        // builder directly so the provider can offer contacts plus Answer-call.
        Game1.game1.ShowTelephoneMenu();
        __result = true;
        return false;
    }

    private static void TelephoneActionFinalizer(Phone? __state) => Instance.telephoneTarget.Value = __state;

    private static bool CallbackQuestionPrefix(GameLocation __instance, Response[] answerChoices,
        GameLocation.afterQuestionBehavior afterDialogueBehavior)
    {
        if (afterDialogueBehavior.Target is not TV tv)
            return true;
        return !Instance.TryShowWorldMenu(new WorldMenuRequest(__instance, tv, answerChoices,
            answer => afterDialogueBehavior(Game1.player, answer.responseKey)));
    }

    private static bool BusTicketActionPrefix(BusStop __instance, xTile.Dimensions.Location tileLocation,
        Farmer who, ref bool __result)
    {
        ModEntry mod = Instance;
        // Match the native ticket-machine branch, including repaired-bus and horse guards.
        // Returning true preserves the original prompt whenever Pam isn't ready to drive.
        if (!mod.CanRouteWorldInteraction || who != Game1.player
            || __instance.getTileIndexAt(tileLocation, "Buildings", "outdoors") != 1057
            || !Game1.MasterPlayer.mailReceived.Contains("ccVault")
            || (who.isRidingHorse() && who.mount != null))
            return true;
        if (!mod.TryShowWorldMenu(new WorldMenuRequest(__instance, __instance, __instance.createYesNoResponses(), answer =>
        {
            __instance.afterQuestion = null;
            __instance.lastQuestionKey = "Bus";
            __instance.answerDialogue(answer);
        }, new Vector2(tileLocation.X, tileLocation.Y))))
            return true;
        __result = true;
        return false;
    }

    private static bool TelephoneDirectoryPrefix(GameLocation __instance,
        List<KeyValuePair<string, string>> responses, Action<string> on_response)
    {
        if (Instance.telephoneTarget.Value is not { } phone)
            return true;
        Response[] choices = responses.Select(pair => new Response(pair.Key, pair.Value)).ToArray();
        return !Instance.TryShowWorldMenu(new WorldMenuRequest(__instance, phone, choices,
            answer => on_response(answer.responseKey)));
    }

    private bool TryShowWorldMenu(WorldMenuRequest request)
    {
        if (!CanRouteWorldInteraction)
            return false;
        try
        {
            WorldMenu? menu = worldContextMenus.Create(request);
            if (menu == null)
                return false;
            Farmer farmer = Game1.player;
            Point origin = farmer.TilePoint;
            uint day = Game1.stats.DaysPlayed;
            bool IsValid() => UsePelicom && Context.IsPlayerFree && !worldInteractionsFailed.Value
                && Game1.player == farmer && Game1.currentLocation == request.Location
                && Game1.stats.DaysPlayed == day && farmer.TilePoint == origin && menu.IsValid();
            ContextMenuItem[] guardedItems = menu.Items.Select(item => item with
            {
                OnClick = () =>
                {
                    if (!IsValid())
                        return;
                    try
                    {
                        item.OnClick?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        OnWorldInteractionFailed(ex);
                    }
                }
            }).ToArray();
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Use the native resolved entity or action tile, never a BusStop location
            // masquerading as an item or a subject selected at the popup's current cursor.
            object? subject = request.LookupTile is { } tile
                ? lookupAnything.GetTileSubject(request.Location, tile)
                : lookupAnything.GetEntitySubject(request.Target, request.Location);
            ContextMenuInfo? info = subject == null ? null : new ContextMenuInfo(() => subject, IsValid);
            if (!windowManager.Value.ShowContextMenu(cursor, guardedItems, "Choose Option", IsValid, info))
                return false;
            // Suppress held repeats after vanilla has resolved this initial right-click.
            Helper.Input.Suppress(SButton.MouseRight);
            return true;
        }
        catch (Exception ex)
        {
            OnWorldInteractionFailed(ex);
            return false;
        }
    }

    private void OnWorldInteractionFailed(Exception ex)
    {
        worldInteractionsFailed.Value = true;
        windowManager.Value.CloseContextMenu();
        Monitor.Log($"World right-click menu failed; restored vanilla interactions for this screen.\n{ex}", LogLevel.Error);
    }
}

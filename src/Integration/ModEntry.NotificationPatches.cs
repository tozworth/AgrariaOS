using AgrariaOS.Notifications;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void InstallNotificationPatches()
    {
        try
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.addHUDMessage), new[] { typeof(HUDMessage) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(CaptureNotificationPrefix)));
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(HUDMessage), nameof(HUDMessage.draw),
                    new[] { typeof(SpriteBatch), typeof(int), typeof(int).MakeByRefType() }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawNotificationPrefix)));
            notificationPatchInstalled = true;
            Monitor.Log("Notification bubbles and session history ready.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't replace notifications; keeping vanilla bubbles.\n{ex}", LogLevel.Error);
        }
    }

    private static void CaptureNotificationPrefix(HUDMessage message)
    {
        ModEntry mod = Instance;
        // Continue recording text-only arrivals while F8 shows vanilla.
        if (!mod.notificationPatchInstalled || !mod.config.Enabled || !Context.IsWorldReady
            || message == null || !NotificationHistory.IsTextOnly(message))
            return;
        try
        {
            mod.notificationHistory.Value.Record(message);
        }
        catch (Exception ex)
        {
            mod.notificationPatchInstalled = false;
            mod.Monitor.Log($"Notification capture failed; restored vanilla bubbles.\n{ex}", LogLevel.Error);
        }
    }

    private static bool DrawNotificationPrefix(HUDMessage __instance, SpriteBatch b, ref int heightUsed)
    {
        ModEntry mod = Instance;
        if (!mod.notificationPatchInstalled || !mod.UsePelicom || !NotificationHistory.IsTextOnly(__instance))
            return true;
        int previousHeight = heightUsed;
        try
        {
            mod.notificationBubbleRenderer.Value.Draw(b, __instance, ref heightUsed, mod.config,
                mod.pelicom.Value.NotificationBounds);
            return false;
        }
        catch (Exception ex)
        {
            heightUsed = previousHeight;
            mod.osHudActive.Value = false;
            mod.Monitor.Log($"Notification drawing failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
            return true;
        }
    }
}

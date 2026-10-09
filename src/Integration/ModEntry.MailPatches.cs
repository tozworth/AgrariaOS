using AgrariaOS.Mail;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private MailArchive mailArchive = null!;

    private void InstallMailPatches()
    {
        mailArchive = new MailArchive(LogMailError);
        MailArchive.Active = mailArchive;
        try
        {
            new Harmony(ModManifest.UniqueID).Patch(
                original: AccessTools.Constructor(typeof(LetterViewerMenu), new[] { typeof(string), typeof(string), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(CaptureMailPostfix)));
            Monitor.Log("Mail archive ready; mailbox deliveries and attachments are saved per farmer.", LogLevel.Info);
        }
        catch (Exception ex) { LogMailError(ex); }
    }

    private static void CaptureMailPostfix(LetterViewerMenu __instance, string mail, bool fromCollection)
    {
        if (fromCollection || !Context.IsWorldReady || !Instance.config.Enabled || Instance.mailArchive.OpeningDesktop) return;
        try { Instance.mailArchive.Capture(__instance, mail); }
        catch (Exception ex) { Instance.LogMailError(ex); }
    }

    private void SyncMail()
    {
        if (!config.Enabled) return;
        try { mailArchive.Sync(); }
        catch (Exception ex) { LogMailError(ex); }
    }

    private void LogMailError(Exception ex)
        => Monitor.Log($"Mail archive operation failed; vanilla mail remains available.\n{ex}", LogLevel.Error);
}

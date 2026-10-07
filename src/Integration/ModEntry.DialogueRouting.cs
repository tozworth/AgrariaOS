using AgrariaOS.Chat;
using StardewModdingAPI;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void RouteDialogue()
    {
        // F8 and disabled HUD preserve vanilla dialogue. Events may hide the rail,
        // but their text still advances through the game's normal dialogue completion.
        if (!UsePelicom || dialogueRoutingFailed.Value)
            return;
        try
        {
            if (DialogueRouter.Advance(chatHistory.Value.Local.Record))
                windowManager.Value.OpenChat(chatHistory.Value, showLocal: true);
        }
        catch (Exception ex)
        {
            dialogueRoutingFailed.Value = true;
            osHudActive.Value = false;
            windowManager.Value.InvalidateLayout();
            Monitor.Log($"Dialogue routing failed; restored vanilla dialogue and HUD.\n{ex}", LogLevel.Error);
        }
    }
}

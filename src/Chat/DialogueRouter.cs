using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Chat;

internal static class DialogueRouter
{
    // Run after the game update, once callers have finished setting up movement, event state,
    // rewards and afterDialogues. Every advancement uses the same path as a player click.
    internal static bool Advance(Action<string, string> record)
    {
        if (Game1.activeClickableMenu is not DialogueBox initialBox
            || initialBox.isQuestion || initialBox.characterDialogue?.isCurrentDialogueAQuestion() == true)
            return false;
        bool recorded = false;
        var captured = new HashSet<(DialogueBox Box, Dialogue? Dialogue, int Index)>();
        for (int step = 0; step < 4096; step++)
        {
            if (Game1.activeClickableMenu is not DialogueBox box)
                return recorded;
            Dialogue? dialogue = box.characterDialogue;
            // Never finish typing, select an answer, or mutate an unanswered choice.
            if (box.isQuestion || dialogue?.isCurrentDialogueAQuestion() == true)
                return recorded;

            if (box.transitioning && !box.transitioningBigger)
            {
                box.closeDialogue();
                continue;
            }
            if (box.dialogueFinished || (dialogue == null && box.dialogues.Count == 0))
            {
                box.closeDialogue();
                continue;
            }

            // Plain boxes remove the first string after each click. Remaining count distinguishes
            // identical consecutive pages; NPC lines use their native index across display pages.
            int index = dialogue?.currentDialogueIndex ?? box.dialogues.Count;
            if (captured.Add((box, dialogue, index)))
            {
                // Capture the whole prepared line once, even if vanilla splits it into several pages.
                string rawText = dialogue?.getCurrentDialogue() ?? box.dialogues[0];
                string text = string.Join(" ", rawText.Replace('^', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (text.Length > 0)
                {
                    record(dialogue?.speaker?.displayName ?? "System", text);
                    recorded = true;
                }
            }
            box.transitioning = false;
            box.safetyTimer = 0;
            box.questionFinishPauseTimer = 0;
            box.finishTyping();
            box.receiveLeftClick(0, 0, playSound: false);
        }
        throw new InvalidOperationException("Dialogue didn't finish advancing after 4096 pages.");
    }
}

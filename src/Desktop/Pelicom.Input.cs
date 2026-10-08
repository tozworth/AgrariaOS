using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

partial class Pelicom : IClickableMenu
{
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (!IsInteractive || !drawnBounds.Contains(x, y))
            return;
        if (drawnSlots[OverviewEntryIndex].Contains(x, y))
            openOverview();
        else if (drawnSlots[MailEntryIndex].Contains(x, y))
            openMail();
        else if (drawnSlots[InventoryEntryIndex].Contains(x, y))
            openInventory();
        else if (drawnSlots[LivestockEntryIndex].Contains(x, y))
            openLivestock();
        else if (drawnSlots[SocialEntryIndex].Contains(x, y))
            openSocial();
        else if (drawnSlots[MiningEntryIndex].Contains(x, y))
            openMiningLedger();
        else if (drawnSlots[BundlesEntryIndex].Contains(x, y))
            openBundles();
        else if (drawnSlots[WalletEntryIndex].Contains(x, y))
            openWallet();
        else if (drawnSlots[NotificationsEntryIndex].Contains(x, y))
            openNotifications();
        else if (drawnSlots[ChatEntryIndex].Contains(x, y))
            openChat();
        else if (drawnSlots[NotesEntryIndex].Contains(x, y))
            openNotes();
        else if (drawnSlots[ExitEntryIndex].Contains(x, y))
            // Terminate immediately without entering the game's save or shutdown flow.
            Environment.Exit(0);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (isWithinBounds(x, y))
            showContextMenu(new Point(x, y), new[]
            {
                new ContextMenuItem("Auto-hide", () => autoHideChecked = !autoHideChecked, autoHideChecked),
                new ContextMenuItem("Position", Children: Enum.GetValues<PelicomEdge>().Select(edge =>
                    new ContextMenuItem(edge.ToString(), () => SetPosition(edge), GetPosition(config) == edge)).ToArray())
            });
    }

}

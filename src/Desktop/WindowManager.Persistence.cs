using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Notifications;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace AgrariaOS.Desktop;

partial class WindowManager
{
    internal void RestoreSavedWindows(NotificationHistory notifications, ChatSession chat, MiningLedger ledger)
    {
        if (!Context.IsWorldReady)
            return;
        long farmerId;
        try { farmerId = Game1.player.UniqueMultiplayerID; }
        catch { return; }
        if (!config.OpenWindows.TryGetValue(farmerId, out List<WindowOpenState>? saved) || saved == null || saved.Count == 0)
            return;
        suppressOpenSave = true;
        try
        {
            foreach (WindowOpenState state in saved.ToArray())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Id) || !PersistentWindowIds.Contains(state.Id))
                    continue;
                try
                {
                    switch (state.Id)
                    {
                        case OverviewWindow.WindowId: OpenOverview(); break;
                        case MailWindow.WindowId: OpenMail(); break;
                        case InventoryWindow.WindowId: OpenInventory(); break;
                        case LivestockWindow.WindowId: OpenLivestock(); break;
                        case SocialWindow.WindowId: OpenSocial(); break;
                        case MiningLedgerWindow.WindowId: OpenMiningLedger(ledger); break;
                        case BundlesWindow.WindowId: OpenBundles(); break;
                        case NotificationsWindow.WindowId: OpenNotifications(notifications); break;
                        case ChatWindow.WindowId: OpenChat(chat); break;
                        case NotesWindow.WindowId: OpenNotes(); break;
                        default: continue;
                    }
                    if (state.IsMinimized && windows.FirstOrDefault(window => window.Id == state.Id) is { } opened)
                        opened.IsMinimized = true;
                }
                catch { }
            }
            RestoreTabGroups(farmerId);
            FocusWorld();
        }
        finally
        {
            suppressOpenSave = false;
        }
        RememberOpenWindows();
        RememberTabGroups();
    }

    private void RememberOpenWindows()
    {
        if (suppressOpenSave || !Context.IsWorldReady)
            return;
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            List<WindowOpenState> current = windows
                .Where(window => window.IsValid && PersistentWindowIds.Contains(window.Id))
                .Select(window => new WindowOpenState { Id = window.Id, IsMinimized = window.IsMinimized })
                .ToList();
            if (config.OpenWindows.TryGetValue(farmerId, out List<WindowOpenState>? saved) && saved != null
                && saved.Count == current.Count && saved.Zip(current).All(pair =>
                    pair.First.Id == pair.Second.Id && pair.First.IsMinimized == pair.Second.IsMinimized))
                return;
            config.OpenWindows[farmerId] = current;
            saveConfig();
        }
        catch { }
    }

    private void RememberTabGroups()
    {
        if (suppressOpenSave || !Context.IsWorldReady)
            return;
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            var current = new List<TabGroupState>();
            foreach (WindowTabGroup group in tabGroups)
            {
                if (group.Tabs.Count < 2)
                    continue;
                if (group.Tabs.Any(member => !member.IsValid || !windows.Contains(member)))
                    continue;
                current.Add(new TabGroupState
                {
                    WindowIds = group.Tabs.Select(member => member.Id).ToList(),
                    ActiveId = group.Active.Id
                });
            }
            if (config.WindowTabGroups.TryGetValue(farmerId, out List<TabGroupState>? saved) && saved != null
                && saved.Count == current.Count && saved.Zip(current).All(pair =>
                    string.Equals(pair.First.ActiveId, pair.Second.ActiveId, StringComparison.Ordinal)
                    && pair.First.WindowIds.Count == pair.Second.WindowIds.Count
                    && pair.First.WindowIds.Zip(pair.Second.WindowIds).All(ids => string.Equals(ids.First, ids.Second, StringComparison.Ordinal))))
                return;
            config.WindowTabGroups[farmerId] = current;
            saveConfig();
        }
        catch { }
    }

    private void RestoreTabGroups(long farmerId)
    {
        if (!config.WindowTabGroups.TryGetValue(farmerId, out List<TabGroupState>? saved) || saved == null)
            return;
        tabGroups.Clear();
        foreach (TabGroupState state in saved.ToArray())
        {
            try
            {
                if (state?.WindowIds == null || state.WindowIds.Count < 2)
                    continue;
                var members = new List<OsWindow>();
                foreach (string id in state.WindowIds)
                {
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    OsWindow? window = windows.FirstOrDefault(candidate => candidate.Id == id && !members.Contains(candidate)
                        && FindGroup(candidate) == null && candidate.IsValid);
                    if (window != null)
                        members.Add(window);
                }
                if (members.Count < 2)
                    continue;
                OsWindow active = members.FirstOrDefault(member => string.Equals(member.Id, state.ActiveId, StringComparison.Ordinal)) ?? members[0];
                WindowPlacement? placement = members.Select(member => member.GetPlacement()).FirstOrDefault(placement => placement != null)
                    ?? active.GetPlacement();
                var group = new WindowTabGroup(members, active, placement ?? new WindowPlacement { X = 0, Y = 0, Width = 420, Height = 260 });
                // Members already carry the saved shared placement; the next draw syncs bounds.
                tabGroups.Add(group);
                group.SetActive(active);
                bool minimized = members.All(member => member.IsMinimized);
                if (!minimized && members.Any(member => member.IsMinimized))
                {
                    // A partially minimized restore (e.g. from an older config) heals to visible.
                    foreach (OsWindow member in members)
                        member.IsMinimized = false;
                }
            }
            catch { }
        }
    }

    private void RememberPlacement(OsWindow window)
    {
        WindowPlacement? placement = window.GetPlacement();
        if (layouts == null || placement == null
            || (layouts.TryGetValue(window.Id, out WindowPlacement? previous) && previous == placement))
            return;

        layouts[window.Id] = placement;
        saveConfig();
    }

    private void RememberGroupPlacement(WindowTabGroup group)
    {
        foreach (OsWindow member in group.Tabs)
            RememberPlacement(member);
    }

    private void DissolveGroupIfSingle(WindowTabGroup group)
    {
        if (group.Tabs.Count < 2)
            tabGroups.Remove(group);
    }

}

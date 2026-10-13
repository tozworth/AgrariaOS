using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

// Desktop journal: same quests as the vanilla QuestLog, shown as a flat
// sortable grid with the selected quest reading in the pane to the right.
// Reward claims, cancellations and read tracking run through the same
// IQuest methods as vanilla so behaviour stays identical.
internal sealed class JournalWindow : OsWindow
{
    internal const string WindowId = "Journal";
    internal const int JournalIcon = 47;
    private const int Padding = GridView<JournalRow>.Padding;
    private const int ScrollbarWidth = 10;
    private const int Divider = 4;
    private static readonly Color GainColor = new(137, 221, 151);
    private static readonly Color LossColor = new(235, 130, 130);
    private static readonly Color NewColor = new(238, 210, 126);

    private readonly GridView<JournalRow> grid = new(
        GridColumn<JournalRow>.Create("Quest", 250, 60, row => row.Title, row => row.Title,
            comparer: StringComparer.CurrentCultureIgnoreCase, id: "quest"),
        GridColumn<JournalRow>.Create("Status", 90, 44, row => row.StatusText, row => row.StatusSort,
            drawCell: DrawStatusCell, id: "status"),
        GridColumn<JournalRow>.Create("Time", 80, 40, row => row.TimeText, row => row.TimeSort,
            textColor: UiStyle.Muted, id: "time"),
        GridColumn<JournalRow>.Create("Reward", 96, 44, row => row.RewardText, row => row.RewardSort,
            rightAligned: true, drawCell: DrawRewardCell, id: "reward"));

    private IQuest? selectedQuest;
    private Rectangle listBounds = Rectangle.Empty;
    private Rectangle previewBounds = Rectangle.Empty;
    private Rectangle previewRowsBounds = Rectangle.Empty;
    private Rectangle previewScrollTrack = Rectangle.Empty;
    private Rectangle previewScrollThumb = Rectangle.Empty;
    private Rectangle actionBounds = Rectangle.Empty;
    private Rectangle claimBounds = Rectangle.Empty;
    private Rectangle dismissBounds = Rectangle.Empty;
    private Rectangle cancelBounds = Rectangle.Empty;
    private readonly List<string> previewLines = new();
    private string previewTitle = "";
    private int previewFirstLine;
    private int previewVisibleLines;
    private int? previewScrollDragOffset;

    internal override string Id => WindowId;
    internal override string Title => "Journal";
    internal override string TitleSubtext
    {
        get
        {
            try
            {
                JournalRow[] rows = GetRows();
                if (rows.Length == 0)
                    return "";
                int fresh = rows.Count(row => row.IsNew);
                int done = rows.Count(row => row.IsComplete);
                string total = rows.Length == 1 ? "1 quest" : $"{rows.Length} quests";
                if (fresh > 0 && done > 0)
                    return $"{total} · {fresh} new · {done} complete";
                if (fresh > 0)
                    return $"{total} · {fresh} new";
                if (done > 0)
                    return $"{total} · {done} complete";
                return total;
            }
            catch { return ""; }
        }
    }
    internal override int Icon => JournalIcon;
    internal override Point DefaultSize => new(760, 480);

    protected override Point GetMinimumSize(ModConfig config)
    {
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int listMinHeight = Padding * 2 + rowHeight * 3 + 1;
        int previewMinHeight = Padding * 2 + lineHeight + 4 + lineHeight * 3;
        const int listMinWidth = 240;
        const int previewMinWidth = 200;
        return new(listMinWidth + Divider + previewMinWidth,
            TitleHeight + 2 + Math.Max(listMinHeight, previewMinHeight));
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        JournalRow[] rows;
        try { rows = GetRows(); }
        catch { rows = Array.Empty<JournalRow>(); }
        grid.SetRows(rows);
        if (rows.Length == 0)
        {
            selectedQuest = null;
            previewLines.Clear();
            previewTitle = "";
            previewFirstLine = 0;
        }
        else if (selectedQuest == null || !rows.Any(row => ReferenceEquals(row.Quest, selectedQuest)))
        {
            selectedQuest = rows[0].Quest;
            previewFirstLine = 0;
            previewScrollDragOffset = null;
        }

        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale);
        const int listMinWidth = 240;
        const int previewMinWidth = 200;
        listBounds = previewBounds = previewRowsBounds = previewScrollTrack = previewScrollThumb
            = actionBounds = claimBounds = dismissBounds = cancelBounds = Rectangle.Empty;
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return;
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { cursor = Point.Zero; }

        if (contentBounds.Width < listMinWidth + Divider + previewMinWidth)
        {
            listBounds = contentBounds;
        }
        else
        {
            int listWidth = (contentBounds.Width - Divider) * 55 / 100;
            listWidth = Math.Clamp(listWidth, listMinWidth, contentBounds.Width - Divider - previewMinWidth);
            listBounds = new Rectangle(contentBounds.X, contentBounds.Y, listWidth, contentBounds.Height);
            previewBounds = new Rectangle(contentBounds.X + listWidth + Divider, contentBounds.Y,
                contentBounds.Width - listWidth - Divider, contentBounds.Height);
        }

        grid.Draw(batch, listBounds, rowHeight, config, cursor, "No quests.",
            isSelected: row => ReferenceEquals(row.Quest, selectedQuest));
        if (previewBounds.IsEmpty)
            return;
        batch.Draw(Game1.staminaRect, new Rectangle(listBounds.Right + 1, contentBounds.Y,
            1, contentBounds.Height), UiStyle.Border * config.Opacity);
        DrawPreview(batch, rows, cursor, config);
    }

    private void DrawPreview(SpriteBatch batch, JournalRow[] rows, Point cursor, ModConfig config)
    {
        previewRowsBounds = previewScrollTrack = previewScrollThumb = Rectangle.Empty;
        actionBounds = claimBounds = dismissBounds = cancelBounds = Rectangle.Empty;
        if (rows.Length == 0)
        {
            DrawCenteredText(batch, "No quests.", previewBounds, UiStyle.Muted, config);
            return;
        }
        JournalRow? selected = rows.FirstOrDefault(row => ReferenceEquals(row.Quest, selectedQuest));
        if (selected == null)
        {
            DrawCenteredText(batch, "Select a quest.", previewBounds, UiStyle.Muted, config);
            return;
        }
        IQuest quest = selected.Quest;

        int lineHeight = UiText.LineHeight(config.FontScale);
        int rowWidth = Math.Max(0, previewBounds.Width - Padding * 2 - ScrollbarWidth);
        RefreshPreview(selected, Math.Max(0, rowWidth - 1), config);
        int headerHeight = lineHeight + 4;

        bool canClaim = false;
        bool canCancel = false;
        bool canDismiss = false;
        string claimText = "";
        try
        {
            canClaim = quest.ShouldDisplayAsComplete() && quest.HasMoneyReward();
            if (canClaim)
            {
                int amount = quest.GetMoneyReward();
                claimText = $"Claim · {amount.ToString("N0", CultureInfo.InvariantCulture)} g";
            }
            else
            {
                canCancel = quest.CanBeCancelled() && !quest.ShouldDisplayAsComplete();
                if (!canCancel && quest.ShouldDisplayAsComplete() && !quest.HasReward())
                    canDismiss = quest is Quest;
                else if (!canCancel && quest.ShouldDisplayAsComplete() && quest.HasReward() && !quest.HasMoneyReward())
                    canDismiss = quest is Quest;
            }
        }
        catch { canClaim = canCancel = canDismiss = false; }

        int actionHeight = (canClaim || canCancel || canDismiss) ? Math.Max(26, lineHeight + Padding * 2) : 0;
        if (actionHeight > 0)
        {
            actionBounds = new Rectangle(previewBounds.X, previewBounds.Bottom - actionHeight,
                previewBounds.Width, actionHeight);
            DrawActions(batch, claimText, canClaim, canCancel, canDismiss, config, cursor);
        }
        int availableHeight = Math.Max(0, previewBounds.Height - Padding * 2 - headerHeight - 4 - actionHeight);
        previewVisibleLines = availableHeight / Math.Max(1, lineHeight);
        previewFirstLine = Math.Clamp(previewFirstLine, 0, MaxPreviewFirstLine);
        previewRowsBounds = new Rectangle(previewBounds.X + Padding,
            previewBounds.Y + Padding + headerHeight + 4, rowWidth, previewVisibleLines * lineHeight);
        previewScrollTrack = new Rectangle(previewRowsBounds.Right + 3, previewRowsBounds.Y,
            ScrollbarWidth - 4, previewRowsBounds.Height);
        if (MaxPreviewFirstLine > 0 && previewScrollTrack.Height > 0)
        {
            int height = Math.Min(previewScrollTrack.Height,
                Math.Max(16, previewScrollTrack.Height * previewVisibleLines / Math.Max(1, previewLines.Count)));
            int y = previewScrollTrack.Y + (int)Math.Round(
                (previewScrollTrack.Height - height) * (double)previewFirstLine / MaxPreviewFirstLine);
            previewScrollThumb = new Rectangle(previewScrollTrack.X, y, previewScrollTrack.Width, height);
            batch.Draw(Game1.staminaRect, previewScrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, previewScrollThumb, UiStyle.Muted * config.Opacity);
        }

        string title = UiText.Fit(previewTitle, Math.Max(0, previewBounds.Width - Padding * 2 - 1), config.FontScale);
        if (!string.IsNullOrEmpty(title) && lineHeight + 1 <= headerHeight)
            UiText.DrawBold(batch, title, new Vector2(previewBounds.X + Padding, previewBounds.Y + Padding),
                UiStyle.Primary, config);
        for (int i = previewFirstLine; i < Math.Min(previewLines.Count, previewFirstLine + previewVisibleLines); i++)
        {
            string line = previewLines[i];
            if (string.IsNullOrEmpty(line))
                continue;
            int y = previewRowsBounds.Y + (i - previewFirstLine) * lineHeight;
            UiText.Draw(batch, line, new Vector2(previewRowsBounds.X, y), UiStyle.Primary, config);
        }
    }

    private int MaxPreviewFirstLine => Math.Max(0, previewLines.Count - previewVisibleLines);

    private void RefreshPreview(JournalRow selected, int textWidth, ModConfig config)
    {
        // Rebuild every frame so objective progress, timers and rewards stay
        // live. Preserve the scroll offset unless the selection changed.
        IQuest quest = selected.Quest;
        bool selectionChanged = !ReferenceEquals(quest, refreshQuest);
        int previousFirstLine = previewFirstLine;
        refreshQuest = quest;
        previewLines.Clear();
        try { previewTitle = quest.GetName() ?? ""; }
        catch { previewTitle = ""; }
        if (textWidth <= 0)
        {
            previewFirstLine = selectionChanged ? 0 : previousFirstLine;
            return;
        }

        var paragraphs = new List<string>();
        try
        {
            if (quest.IsTimedQuest() && quest.GetDaysLeft() > 0)
                paragraphs.Add(GetDaysLeftText(quest));
        }
        catch { }
        try
        {
            string description = quest.GetDescription() ?? "";
            foreach (string part in description.Replace('^', '\n').Split('\n'))
            {
                string cleaned = string.Join(" ", part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrEmpty(cleaned))
                    paragraphs.Add(cleaned);
            }
        }
        catch { }
        try
        {
            List<string> objectives = quest.GetObjectiveDescriptions() ?? new();
            SpecialOrder? order = quest as SpecialOrder;
            for (int i = 0; i < objectives.Count; i++)
            {
                string objective = objectives[i]?.Trim() ?? "";
                if (string.IsNullOrEmpty(objective))
                    continue;
                if (order != null && i < order.objectives.Count)
                {
                    var detail = order.objectives[i];
                    try
                    {
                        if (detail.GetMaxCount() > 1 && detail.ShouldShowProgress())
                            objective += $" ({detail.GetCount()}/{detail.GetMaxCount()})";
                        else if (detail.IsComplete())
                            objective += " (Done)";
                    }
                    catch { }
                }
                paragraphs.Add("- " + objective);
            }
        }
        catch { }
        try
        {
            if (quest.ShouldDisplayAsComplete())
            {
                string complete = GetCompleteText();
                if (!string.IsNullOrEmpty(complete))
                    paragraphs.Add(complete);
                if (quest.HasMoneyReward())
                {
                    int amount = quest.GetMoneyReward();
                    paragraphs.Add($"Reward: {amount.ToString("N0", CultureInfo.InvariantCulture)} g");
                }
                else if (quest is Quest vanilla && !string.IsNullOrEmpty(vanilla.rewardDescription.Value)
                    && vanilla.rewardDescription.Value.Length > 2)
                {
                    paragraphs.Add($"Reward: {vanilla.rewardDescription.Value}");
                }
            }
            else if (quest.GetMoneyReward() > 0)
            {
                // Show the pending payout like the vanilla detail does on completion.
                int amount = quest.GetMoneyReward();
                paragraphs.Add($"Reward: {amount.ToString("N0", CultureInfo.InvariantCulture)} g");
            }
            else if (quest is Quest pending && !string.IsNullOrEmpty(pending.rewardDescription.Value)
                && pending.rewardDescription.Value.Length > 2)
            {
                paragraphs.Add($"Reward: {pending.rewardDescription.Value}");
            }
        }
        catch { }

        foreach (string paragraph in paragraphs)
        {
            foreach (string line in UiText.Wrap(paragraph, Math.Max(0, textWidth - 1), config.FontScale))
                previewLines.Add(line);
            previewLines.Add("");
        }
        while (previewLines.Count > 0 && previewLines[^1] == "")
            previewLines.RemoveAt(previewLines.Count - 1);
        previewFirstLine = selectionChanged ? 0 : Math.Clamp(previousFirstLine, 0, MaxPreviewFirstLine);
    }
    private IQuest? refreshQuest;

    private void DrawActions(SpriteBatch batch, string claimText, bool canClaim, bool canCancel, bool canDismiss, ModConfig config, Point cursor)
    {
        batch.Draw(Game1.staminaRect, new Rectangle(actionBounds.X, actionBounds.Y, actionBounds.Width, 1), UiStyle.Border * config.Opacity);
        int x = actionBounds.X + Padding;
        int buttonHeight = Math.Max(0, actionBounds.Height - 4);
        int y = actionBounds.Y + 2;
        if (canClaim)
        {
            int width = (int)MathF.Ceiling(UiText.Measure(claimText).X * config.FontScale) + Padding * 2;
            claimBounds = new Rectangle(x, y, Math.Min(width, Math.Max(0, actionBounds.Right - x)), buttonHeight);
            DrawActionButton(batch, claimBounds, claimText, cursor, config);
        }
        else if (canCancel)
        {
            const string text = "Cancel quest";
            int width = (int)MathF.Ceiling(UiText.Measure(text).X * config.FontScale) + Padding * 2;
            cancelBounds = new Rectangle(x, y, Math.Min(width, Math.Max(0, actionBounds.Right - x)), buttonHeight);
            DrawActionButton(batch, cancelBounds, text, cursor, config);
        }
        else if (canDismiss)
        {
            const string text = "Done";
            int width = (int)MathF.Ceiling(UiText.Measure(text).X * config.FontScale) + Padding * 2;
            dismissBounds = new Rectangle(x, y, Math.Min(width, Math.Max(0, actionBounds.Right - x)), buttonHeight);
            DrawActionButton(batch, dismissBounds, text, cursor, config);
        }
    }

    private static void DrawActionButton(SpriteBatch batch, Rectangle bounds, string text, Point cursor, ModConfig config)
    {
        if (bounds.IsEmpty)
            return;
        if (bounds.Contains(cursor))
            batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);
        UiStyle.DrawBorder(batch, bounds, config.Opacity);
        DrawCenteredText(batch, text, bounds, UiStyle.Primary, config);
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
        {
            grid.ReceiveClick(cursor, leftClick);
            return;
        }
        if (!claimBounds.IsEmpty && claimBounds.Contains(cursor))
        {
            ClaimSelected();
            return;
        }
        if (!cancelBounds.IsEmpty && cancelBounds.Contains(cursor))
        {
            CancelSelected();
            return;
        }
        if (!dismissBounds.IsEmpty && dismissBounds.Contains(cursor))
        {
            DismissSelected();
            return;
        }
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!listBounds.IsEmpty && listBounds.Contains(cursor))
        {
            JournalRow? row = grid.GetRow(cursor);
            if (row != null)
            {
                IQuest? previous = selectedQuest;
                if (!ReferenceEquals(previous, row.Quest))
                {
                    // Leaving the previous detail dismisses reward-less completions,
                    // exactly as exiting the vanilla quest page does.
                    if (previous != null)
                    {
                        try { previous.OnLeaveQuestPage(); }
                        catch { }
                    }
                    selectedQuest = row.Quest;
                    previewFirstLine = 0;
                    previewScrollDragOffset = null;
                }
                // Mark viewed even when re-clicking the auto-selected row, so the
                // first explicit click clears the New flag like vanilla does.
                try { row.Quest.MarkAsViewed(); }
                catch { }
                try { Game1.dayTimeMoneyBox.DismissQuestPing(); }
                catch { }
            }
            return;
        }
        if (!previewScrollThumb.IsEmpty && previewScrollTrack.Contains(cursor))
        {
            previewScrollDragOffset = previewScrollThumb.Contains(cursor)
                ? cursor.Y - previewScrollThumb.Y : previewScrollThumb.Height / 2;
            DragContent(cursor);
        }
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);
    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);

    internal override void DragContent(Point cursor)
    {
        grid.Drag(cursor);
        if (previewScrollDragOffset.HasValue && previewScrollTrack.Height > previewScrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - previewScrollDragOffset.Value - previewScrollTrack.Y)
                / (previewScrollTrack.Height - previewScrollThumb.Height);
            previewFirstLine = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxPreviewFirstLine);
        }
    }

    internal override void EndContentDrag()
    {
        grid.EndDrag();
        previewScrollDragOffset = null;
    }

    internal override void ReceiveScroll(int delta)
    {
        if (delta == 0)
            return;
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { return; }
        if (!listBounds.IsEmpty && listBounds.Contains(cursor))
        {
            grid.ReceiveScroll(delta);
            return;
        }
        if (!previewBounds.IsEmpty && previewBounds.Contains(cursor))
        {
            int steps = Math.Max(1, Math.Abs(delta) / 120);
            previewFirstLine = (int)Math.Clamp((long)previewFirstLine - Math.Sign(delta) * steps * 3, 0, MaxPreviewFirstLine);
        }
    }

    private void ClaimSelected()
    {
        if (selectedQuest == null)
            return;
        try
        {
            IQuest quest = selectedQuest;
            if (!quest.ShouldDisplayAsComplete() || !quest.HasMoneyReward())
                return;
            int reward = quest.GetMoneyReward();
            if (reward > 0)
                Game1.player.Money += reward;
            Game1.playSound("purchaseRepeat");
            quest.OnMoneyRewardClaimed();
            try { quest.OnLeaveQuestPage(); }
            catch { }
            previewFirstLine = 0;
            previewScrollDragOffset = null;
        }
        catch { }
    }

    private void CancelSelected()
    {
        if (selectedQuest == null)
            return;
        try
        {
            IQuest quest = selectedQuest;
            if (quest.ShouldDisplayAsComplete() || !quest.CanBeCancelled())
                return;
            if (quest is Quest vanilla)
            {
                vanilla.accepted.Value = false;
                if (vanilla.dailyQuest.Value && vanilla.dayQuestAccepted.Value == Game1.Date.TotalDays)
                    Game1.player.acceptedDailyQuest.Set(newValue: false);
                Game1.player.questLog.Remove(vanilla);
                Game1.playSound("trashcan");
                previewFirstLine = 0;
                previewScrollDragOffset = null;
            }
        }
        catch { }
    }

    private void DismissSelected()
    {
        if (selectedQuest == null)
            return;
        try
        {
            selectedQuest.OnLeaveQuestPage();
            previewFirstLine = 0;
            previewScrollDragOffset = null;
        }
        catch { }
    }

    // Mirrors QuestLog.GetAllQuests: special orders then the quest log, newest
    // first, skipping hidden entries and pruning destroyed quests.
    private static JournalRow[] GetRows()
    {
        if (!Context.IsWorldReady)
            return Array.Empty<JournalRow>();
        var all = new List<IQuest>();
        try
        {
            var orders = Game1.player.team.specialOrders;
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                SpecialOrder order = orders[i];
                if (order == null || order.IsHidden())
                    continue;
                all.Add(order);
            }
        }
        catch { }
        try
        {
            var log = Game1.player.questLog;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                Quest quest;
                try { quest = log[i]; }
                catch { continue; }
                if (quest == null)
                {
                    try { log.RemoveAt(i); }
                    catch { }
                    continue;
                }
                bool destroyed;
                try { destroyed = quest.destroy.Value; }
                catch { continue; }
                if (destroyed)
                {
                    try { log.RemoveAt(i); }
                    catch { }
                    continue;
                }
                bool hidden;
                try { hidden = quest.IsHidden(); }
                catch { continue; }
                if (!hidden)
                    all.Add(quest);
            }
        }
        catch { }
        return all.Select(quest => new JournalRow(quest)).ToArray();
    }

    private static string GetDaysLeftText(IQuest quest)
    {
        int days = quest.GetDaysLeft();
        try
        {
            if (days > 1)
                return Game1.content.LoadString("Strings\\StringsFromCSFiles:QuestLog.cs.11374", days);
            return Game1.content.LoadString("Strings\\StringsFromCSFiles:Quest_FinalDay");
        }
        catch
        {
            return days > 1 ? $"{days} days left" : "Final day";
        }
    }

    private static string GetCompleteText()
    {
        try { return Game1.content.LoadString("Strings\\StringsFromCSFiles:QuestLog.cs.11376"); }
        catch { return "Quest complete!"; }
    }

    private static void DrawStatusCell(SpriteBatch batch, JournalRow row, Rectangle bounds, ModConfig config)
    {
        Color color = row.StatusSort switch
        {
            0 => NewColor,
            2 => GainColor,
            3 => LossColor,
            _ => UiStyle.Muted
        };
        UiText.DrawCell(batch, row.StatusText, bounds, color, config);
    }

    private static void DrawRewardCell(SpriteBatch batch, JournalRow row, Rectangle bounds, ModConfig config)
    {
        Color color = UiStyle.Muted;
        try
        {
            if (row.Quest.ShouldDisplayAsComplete() && row.Quest.HasMoneyReward())
                color = GainColor;
            else if (row.RewardSort.HasValue && row.RewardSort.Value > 0)
                color = UiStyle.Primary;
        }
        catch { }
        UiText.DrawCell(batch, row.RewardText, bounds, color, config, rightAligned: true);
    }

    private sealed class JournalRow
    {
        internal IQuest Quest { get; }
        internal string Title { get; }
        internal string StatusText { get; }
        internal int StatusSort { get; }
        internal bool IsNew { get; }
        internal bool IsComplete { get; }
        internal string TimeText { get; }
        internal int? TimeSort { get; }
        internal string RewardText { get; }
        internal int? RewardSort { get; }

        internal JournalRow(IQuest quest)
        {
            Quest = quest;
            string title = "";
            string status = "";
            int statusSort = 1;
            bool isNew = false;
            bool isComplete = false;
            string timeText = "";
            int? timeSort = null;
            string rewardText = "";
            int? rewardSort = null;
            try { title = quest.GetName() ?? ""; }
            catch { }
            try
            {
                bool complete = quest.ShouldDisplayAsComplete();
                bool fresh = quest.ShouldDisplayAsNew();
                isNew = fresh && !complete;
                isComplete = complete;
                if (complete)
                {
                    if (quest is SpecialOrder order && order.questState.Value == SpecialOrderStatus.Failed)
                    {
                        status = "Failed";
                        statusSort = 3;
                    }
                    else
                    {
                        status = "Complete";
                        statusSort = 2;
                    }
                }
                else if (fresh)
                {
                    status = "New";
                    statusSort = 0;
                }
                else
                {
                    status = "Active";
                    statusSort = 1;
                }
            }
            catch { }
            try
            {
                if (quest.IsTimedQuest())
                {
                    int days = quest.GetDaysLeft();
                    if (days > 0)
                    {
                        timeSort = days;
                        if (days > 1)
                        {
                            try { timeText = Game1.content.LoadString("Strings\\StringsFromCSFiles:QuestLog.cs.11374", days); }
                            catch { timeText = $"{days} days"; }
                        }
                        else
                        {
                            try { timeText = Game1.content.LoadString("Strings\\StringsFromCSFiles:Quest_FinalDay"); }
                            catch { timeText = "Final day"; }
                        }
                    }
                }
            }
            catch { }
            try
            {
                int money = quest.GetMoneyReward();
                if (money > 0)
                {
                    rewardText = $"{money.ToString("N0", CultureInfo.InvariantCulture)} g";
                    rewardSort = money;
                }
                else if (quest.HasReward())
                {
                    if (quest is Quest vanilla && !string.IsNullOrEmpty(vanilla.rewardDescription.Value)
                        && vanilla.rewardDescription.Value.Length > 2)
                    {
                        rewardText = vanilla.rewardDescription.Value;
                        rewardSort = 0;
                    }
                }
            }
            catch { }
            Title = title;
            StatusText = status;
            StatusSort = statusSort;
            IsNew = isNew;
            IsComplete = isComplete;
            TimeText = timeText;
            TimeSort = timeSort;
            RewardText = rewardText;
            RewardSort = rewardSort;
        }
    }
}

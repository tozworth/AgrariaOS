using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using AgrariaOS.Wallet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

internal sealed class WalletWindow : OsWindow
{
    internal const string WindowId = "Wallet";
    private const int Padding = GridView<WalletRow>.Padding;
    private static readonly Color GainColor = new(137, 221, 151);
    private static readonly Color LossColor = new(235, 130, 130);
    private readonly WalletLedger ledger;
    private readonly GridView<WalletRow> grid = new(
        GridColumn<WalletRow>.Create("Date", 150, 70, row => row.DateText, row => row.Order, id: "date"),
        GridColumn<WalletRow>.Create("Type", 80, 40, row => row.Kind, row => row.Kind,
            comparer: StringComparer.CurrentCultureIgnoreCase, textColor: UiStyle.Muted, id: "type"),
        GridColumn<WalletRow>.Create("Description", 260, 60, row => row.Description, row => row.Description,
            comparer: StringComparer.CurrentCultureIgnoreCase, id: "description"),
        GridColumn<WalletRow>.Create("Amount", 90, 44, row => row.AmountText, row => row.Amount,
            rightAligned: true, drawCell: DrawAmountCell, id: "amount"),
        GridColumn<WalletRow>.Create("Balance", 96, 44, row => row.BalanceText, row => row.Balance,
            rightAligned: true, id: "balance"));

    internal WalletWindow(ModConfig config, WalletLedger ledger) => this.ledger = ledger;

    internal override string Id => WindowId;
    internal override string Title => "Wallet";
    internal override string TitleSubtext => GetBalanceText();
    internal override int Icon => 41;
    internal override Point DefaultSize => new(640, 440);

    protected override Point GetMinimumSize(ModConfig config)
        => new(340, TitleHeight + 2 + Padding * 2
            + grid.GetRowHeight(Game1.smallFont, config.FontScale) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        if (!Context.IsWorldReady)
        {
            UiText.DrawCell(batch, "No save loaded.", contentBounds, UiStyle.Muted, config);
            return;
        }
        // The balance lives in the title bar; the body is just the journal.
        long farmerId;
        try
        {
            farmerId = Game1.player.UniqueMultiplayerID;
        }
        catch
        {
            UiText.DrawCell(batch, "No save loaded.", contentBounds, UiStyle.Muted, config);
            return;
        }
        WalletRow[] rows = ledger.GetEntries(farmerId)
            .Select(entry => new WalletRow(entry, config.Use24HourClock)).ToArray();
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        string? footer = rows.Length == 0 ? null
            : rows.Length == 1 ? "1 transaction" : $"{rows.Length} transactions";
        grid.SetRows(rows);
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale),
            config, cursor, "No transactions yet.", footerText: footer);
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
        => grid.ReceiveClick(cursor, leftClick);
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private static string GetBalanceText()
    {
        try
        {
            return Context.IsWorldReady
                ? $"{Game1.player.Money.ToString("N0", CultureInfo.InvariantCulture)} g" : "";
        }
        catch { return ""; }
    }

    private static void DrawAmountCell(SpriteBatch batch, WalletRow row, Rectangle bounds, ModConfig config)
    {
        Color color = row.Amount > 0 ? GainColor : row.Amount < 0 ? LossColor : UiStyle.Muted;
        UiText.DrawCell(batch, row.AmountText, bounds, color, config, rightAligned: true);
    }

    private sealed class WalletRow
    {
        internal long Order { get; }
        internal string DateText { get; }
        internal string Kind { get; }
        internal string Description { get; }
        internal int Amount { get; }
        internal string AmountText { get; }
        internal int Balance { get; }
        internal string BalanceText { get; }

        internal WalletRow(WalletLedger.Entry entry, bool use24HourClock)
        {
            Order = entry.Order;
            DateText = FormatDate(entry, use24HourClock);
            Kind = entry.Kind;
            Description = entry.Description;
            Amount = entry.Amount;
            AmountText = entry.Amount > 0
                ? $"+{entry.Amount.ToString("N0", CultureInfo.InvariantCulture)} g"
                : entry.Amount < 0
                    ? $"-{Math.Abs(entry.Amount).ToString("N0", CultureInfo.InvariantCulture)} g"
                    : "—";
            Balance = entry.BalanceAfter;
            BalanceText = $"{entry.BalanceAfter.ToString("N0", CultureInfo.InvariantCulture)} g";
        }

        private static string FormatDate(WalletLedger.Entry entry, bool use24HourClock)
        {
            string season = string.IsNullOrEmpty(entry.Season) ? "???"
                : char.ToUpperInvariant(entry.Season[0]) + entry.Season[1..];
            string day = entry.Day > 0 ? $" {entry.Day}" : "";
            string time = entry.Time > 0
                ? $" · {StatusReadoutRenderer.FormatTime(entry.Time, use24HourClock)}" : "";
            return $"{season}{day}{time}";
        }
    }
}

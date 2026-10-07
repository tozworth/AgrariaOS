using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

internal sealed class MailWindow : OsWindow
{
    internal const string WindowId = "Mail";
    private const int Padding = GridView<MailRow>.Padding;
    private const int IconSize = 20;
    private readonly GridView<MailRow> grid = new(
        GridColumn<MailRow>.Create("Title", 200, 44, row => row.Title, row => row.Title,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawTitleCell, id: "title"),
        GridColumn<MailRow>.Create("Body", 360, 60, row => row.Preview, row => row.Preview,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "body"));

    internal override string Id => WindowId;
    internal override string Title => "Mail";
    internal override string TitleSubtext
    {
        get
        {
            int count = GetRows().Length;
            return count == 1 ? "1 letter" : $"{count} letters";
        }
    }
    internal override int Icon => UiStyle.MailIcon;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        grid.SetRows(GetRows());
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, "No mail.");
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!leftClick || !Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu != null)
            return;
        // Mirrors CollectionsPage's letters tab: the same LetterViewerMenu vanilla pops.
        MailRow? row = grid.GetRow(cursor);
        if (row == null || !Game1.player.mailReceived.Contains(row.Id))
            return;
        Dictionary<string, string> mail = DataLoader.Mail(Game1.content);
        if (!mail.TryGetValue(row.Id, out string? raw))
            return;
        Game1.activeClickableMenu = new LetterViewerMenu(raw, row.Id, fromCollection: true);
    }
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    internal override void Update()
    {
        // LetterViewerMenu with fromCollection:true reports mouse dismissal via
        // destroy instead of exiting, since vanilla hosts it as a submenu. When we
        // host it as the active menu, translate that flag into a real close.
        if (Game1.activeClickableMenu is LetterViewerMenu viewer && viewer.isFromCollection && viewer.destroy)
            viewer.exitThisMenu(playSound: false);
    }

    // Mirrors CollectionsPage's letters tab: received IDs with mail data, in received
    // order. The title is the "[#]" suffix ("???" when absent); the body is the prefix.
    private static MailRow[] GetRows()
    {
        if (!Context.IsWorldReady)
            return Array.Empty<MailRow>();
        Dictionary<string, string> mail = DataLoader.Mail(Game1.content);
        string playerName = Game1.player.Name;
        Gender gender = Game1.player.Gender;
        var rows = new List<MailRow>();
        foreach (string id in Game1.player.mailReceived)
        {
            if (!mail.TryGetValue(id, out string? raw))
                continue;
            string[] parts = raw.Split("[#]");
            string title = parts.Length > 1 ? parts[1] : "???";
            rows.Add(new MailRow(id, title, GetPreview(parts[0], playerName, gender)));
        }
        return rows.ToArray();
    }

    // Single-line preview of what the vanilla viewer shows: player/gender tokens
    // resolved, custom formatting and item/action commands stripped, line breaks
    // flattened. Secretsanta stays "???" for a stable sort outside Dec 18-25.
    private static string GetPreview(string body, string playerName, Gender gender)
    {
        body = body.Replace("@", playerName);
        body = Dialogue.applyGenderSwitch(gender, body, altTokenOnly: true);
        body = StripCustomFormatting(body);
        body = StripCommands(body, "%action");
        body = StripCommands(body, "%item");
        body = body.Replace("%secretsanta", "???").Replace('^', ' ');
        return string.Join(" ", body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string StripCommands(string text, string command)
    {
        int start = text.IndexOf(command, StringComparison.Ordinal);
        while (start >= 0)
        {
            int end = text.IndexOf("%%", start, StringComparison.Ordinal);
            if (end < 0)
                break;
            text = text.Remove(start, end + 2 - start);
            start = text.IndexOf(command, StringComparison.Ordinal);
        }
        return text;
    }

    // Mirrors LetterViewerMenu.ApplyCustomFormatting tag removal ([letterbg ...]
    // and [textcolor ...]) without touching viewer background or text colors.
    private static string StripCustomFormatting(string text)
    {
        for (int start = text.IndexOf('['); start >= 0; start = text.IndexOf('[', start + 1))
        {
            int end = text.IndexOf(']', start);
            if (end < 0)
                break;
            string first = text.Substring(start + 1, end - start - 1).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (first == "letterbg" || first == "textcolor")
            {
                text = text.Remove(start, end - start + 1);
                start--;
            }
        }
        return text;
    }

    private static void DrawTitleCell(SpriteBatch batch, MailRow row, Rectangle bounds, ModConfig config)
    {
        // Vanilla letters-tab envelope: Game1.mouseCursors (190, 423, 14, 11).
        Rectangle source = new(190, 423, 14, 11);
        float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(Game1.mouseCursors, destination, source, Color.White * config.Opacity);
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Title, bounds, UiStyle.Primary, config);
    }

    private sealed class MailRow
    {
        internal string Id { get; }
        internal string Title { get; }
        internal string Preview { get; }

        internal MailRow(string id, string title, string preview)
        {
            Id = id;
            Title = title;
            Preview = preview;
        }
    }
}

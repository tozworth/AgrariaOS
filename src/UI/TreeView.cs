using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

// Reusable two-level (or deeper) tree: categories with expandable children.
// Windows own the nodes; this view only tracks expansion, selection and scroll.
internal sealed class TreeNode
{
    internal string Id { get; }
    internal string Label { get; }
    internal bool IsComplete { get; }
    internal List<TreeNode> Children { get; } = new();
    internal object? Payload { get; }

    internal TreeNode(string id, string label, bool isComplete = false, object? payload = null)
    {
        Id = id;
        Label = label;
        IsComplete = isComplete;
        Payload = payload;
    }
}

internal sealed class TreeView
{
    internal const int Padding = 4;
    private const int ScrollbarWidth = 10;
    private const int Indent = 14;
    private const int ChevronWidth = 14;

    private readonly List<TreeNode> roots = new();
    private readonly HashSet<string> collapsed = new(StringComparer.Ordinal);
    private readonly List<(TreeNode Node, int Depth)> visible = new();
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int rowHeight;
    private int layoutFirstRow;
    private int layoutEndRow;
    private int? scrollDragOffset;

    internal IReadOnlyList<TreeNode> Roots => roots;
    internal Rectangle RowsBounds { get; private set; }
    internal int FirstRow { get; private set; }
    internal int VisibleRows { get; private set; }
    internal string? SelectedId { get; private set; }
    private int MaxFirstRow => Math.Max(0, visible.Count - VisibleRows);

    internal void SetRoots(IEnumerable<TreeNode> nodes)
    {
        roots.Clear();
        roots.AddRange(nodes);
        ClearLayout();
    }

    internal TreeNode? SelectedNode => SelectedId == null ? null : Find(SelectedId);

    internal TreeNode? Find(string id)
    {
        var stack = new Stack<TreeNode>(roots);
        while (stack.Count > 0)
        {
            TreeNode node = stack.Pop();
            if (string.Equals(node.Id, id, StringComparison.Ordinal))
                return node;
            for (int i = node.Children.Count - 1; i >= 0; i--)
                stack.Push(node.Children[i]);
        }
        return null;
    }

    internal void Select(string? id)
    {
        SelectedId = id;
    }

    internal void EnsureSelection()
    {
        if (SelectedId != null && Find(SelectedId) != null)
            return;
        // Prefer the first leaf (bundle) so the right pane has contents on open.
        var stack = new Stack<TreeNode>();
        for (int i = roots.Count - 1; i >= 0; i--)
            stack.Push(roots[i]);
        TreeNode? firstLeaf = null;
        TreeNode? first = null;
        while (stack.Count > 0)
        {
            TreeNode node = stack.Pop();
            first ??= node;
            if (node.Children.Count == 0 && firstLeaf == null)
                firstLeaf = node;
            bool expanded = !collapsed.Contains(node.Id);
            if (expanded)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                    stack.Push(node.Children[i]);
            }
        }
        SelectedId = (firstLeaf ?? first)?.Id;
    }

    internal bool IsExpanded(TreeNode node) => !collapsed.Contains(node.Id);

    internal bool Layout(Rectangle bounds, int rowHeight)
    {
        if (rowHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowHeight));
        ClearLayout();
        this.rowHeight = rowHeight;
        RebuildVisible();
        int availableHeight = Math.Max(0, bounds.Height - Padding * 2);
        int tableWidth = Math.Max(0, bounds.Width - Padding * 2 - ScrollbarWidth);
        if (availableHeight < rowHeight || tableWidth == 0)
        {
            EndDrag();
            return false;
        }
        VisibleRows = Math.Max(1, availableHeight / rowHeight);
        FirstRow = Math.Clamp(FirstRow, 0, MaxFirstRow);
        layoutFirstRow = FirstRow;
        layoutEndRow = Math.Min(visible.Count, FirstRow + VisibleRows);
        RowsBounds = new Rectangle(bounds.X + Padding, bounds.Y + Padding, tableWidth, availableHeight);
        scrollTrack = new Rectangle(RowsBounds.Right + 2, RowsBounds.Y, ScrollbarWidth - 4, RowsBounds.Height);
        if (MaxFirstRow > 0)
        {
            int thumbHeight = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * VisibleRows / Math.Max(1, visible.Count)));
            int thumbY = scrollTrack.Y + (int)Math.Round((scrollTrack.Height - thumbHeight) * (double)FirstRow / MaxFirstRow);
            scrollThumb = new Rectangle(scrollTrack.X, thumbY, scrollTrack.Width, thumbHeight);
        }
        return true;
    }

    internal void Draw(SpriteBatch batch, Rectangle bounds, int rowHeight, ModConfig config, Point cursor, string emptyText)
    {
        if (!Layout(bounds, rowHeight))
        {
            UiText.DrawCell(batch, "Not enough space.", bounds, UiStyle.Muted, config);
            return;
        }
        for (int i = layoutFirstRow; i < layoutEndRow; i++)
        {
            (TreeNode node, int depth) = visible[i];
            Rectangle row = new(RowsBounds.X, RowsBounds.Y + (i - layoutFirstRow) * rowHeight, RowsBounds.Width, rowHeight);
            bool selected = string.Equals(SelectedId, node.Id, StringComparison.Ordinal);
            if (selected)
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            else if (row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));

            Color color = node.IsComplete ? UiStyle.Muted : UiStyle.Primary;
            int chevronX = row.X + depth * Indent;
            int textX = chevronX;
            if (node.Children.Count > 0)
            {
                string chevron = IsExpanded(node) ? "v" : ">";
                UiText.Draw(batch, chevron, new Vector2(chevronX, row.Y + (row.Height - Game1.smallFont.MeasureString(chevron).Y * config.FontScale) / 2), color, config);
                textX += ChevronWidth;
            }
            // Leaf nodes take the full row, so roots without children render as a flat list.
            Rectangle labelBounds = new(textX, row.Y, Math.Max(0, row.Right - textX), row.Height);
            UiText.DrawCell(batch, node.Label, labelBounds, color, config);
            if (node.IsComplete)
            {
                string fitted = UiText.Fit(node.Label, Math.Max(0, labelBounds.Width - Padding * 2 - 1), config.FontScale);
                Vector2 size = Game1.smallFont.MeasureString(fitted) * config.FontScale;
                if (fitted.Length > 0 && size.X > 0)
                {
                    float y = labelBounds.Y + (labelBounds.Height - 1) / 2f;
                    batch.Draw(Game1.staminaRect, new Rectangle(labelBounds.X + Padding, (int)MathF.Round(y), (int)MathF.Ceiling(size.X), 1), color * config.Opacity);
                }
            }
        }
        if (visible.Count == 0)
        {
            emptyText = UiText.Fit(emptyText, Math.Max(0, RowsBounds.Width - 1), config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(emptyText) * config.FontScale;
            if (emptyText.Length > 0 && size.Y + 1 <= RowsBounds.Height)
                UiText.Draw(batch, emptyText, new Vector2(RowsBounds.X + (RowsBounds.Width - size.X) / 2,
                    RowsBounds.Y + (RowsBounds.Height - size.Y) / 2), UiStyle.Muted, config);
        }
        if (!scrollThumb.IsEmpty)
        {
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }
    }

    // Hit testing for context menus (e.g. right-clicking a row without selecting it first).
    internal TreeNode? GetNodeAt(Point cursor)
    {
        if (RowsBounds.IsEmpty || rowHeight <= 0 || !RowsBounds.Contains(cursor))
            return null;
        int index = layoutFirstRow + (cursor.Y - RowsBounds.Y) / rowHeight;
        if (index < layoutFirstRow || index >= layoutEndRow)
            return null;
        return visible[index].Node;
    }

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return false;
        if (!scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            Drag(cursor);
            return true;
        }
        if (!RowsBounds.Contains(cursor) || rowHeight <= 0)
            return false;
        int index = layoutFirstRow + (cursor.Y - RowsBounds.Y) / rowHeight;
        if (index < 0 || index >= layoutEndRow)
            return false;
        (TreeNode node, int depth) = visible[index];
        Rectangle row = new(RowsBounds.X, RowsBounds.Y + (index - layoutFirstRow) * rowHeight, RowsBounds.Width, rowHeight);
        Rectangle chevronBox = new(row.X + depth * Indent, row.Y, ChevronWidth + Padding, row.Height);
        if (node.Children.Count > 0 && chevronBox.Contains(cursor))
        {
            if (collapsed.Contains(node.Id))
                collapsed.Remove(node.Id);
            else
                collapsed.Add(node.Id);
            SelectedId = node.Id;
            return true;
        }
        SelectedId = node.Id;
        return true;
    }

    internal void Drag(Point cursor)
    {
        if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            FirstRow = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstRow);
        }
    }

    internal void EndDrag() => scrollDragOffset = null;

    internal void ReceiveScroll(int delta)
    {
        if (delta != 0 && !RowsBounds.IsEmpty)
            FirstRow = (int)Math.Clamp((long)FirstRow - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120), 0, MaxFirstRow);
    }

    private void RebuildVisible()
    {
        visible.Clear();
        var stack = new Stack<(TreeNode Node, int Depth)>();
        for (int i = roots.Count - 1; i >= 0; i--)
            stack.Push((roots[i], 0));
        while (stack.Count > 0)
        {
            (TreeNode node, int depth) = stack.Pop();
            visible.Add((node, depth));
            if (!collapsed.Contains(node.Id))
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                    stack.Push((node.Children[i], depth + 1));
            }
        }
    }

    private void ClearLayout()
    {
        RowsBounds = scrollTrack = scrollThumb = Rectangle.Empty;
        VisibleRows = layoutFirstRow = layoutEndRow = 0;
    }
}

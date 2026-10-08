# Shared grid views

Use `UI/GridView<TRow>` for tabular window content. `TRow` is a reference type;
it can be a game object or a window-specific row model. Inventory and chests use
`GridView<Item>`. A Livestock window can supply its own animal rows and columns.

Define columns with `GridColumn<TRow>.Create<TKey>`. Each column supplies a heading,
preferred/minimum width, display text, and a typed sort key. Use numeric keys for
quantities, prices, or animal stats, and `StringComparer.CurrentCultureIgnoreCase`
for localized text. Null keys stay last in both directions. Equal keys retain
source order. Supply a unique, stable `id` for each column so saved visibility
survives heading changes or localization, along with saved widths; the heading is
the default ID.
A custom `drawCell` callback can render an icon or badge within its
cell bounds; ordinary cells use the common pixel-aligned text renderer.

In the window's `DrawContent`:

1. Update any context captured by columns (e.g. the Price Catalogue unlock).
2. Call `SetRows(liveSource)` to refresh and sort a snapshot. The source is never
   reordered. Pass source rows in their natural order on every refresh.
3. Refresh window-owned selection against `grid.Rows`.
4. Call `Draw` with content bounds, row height, config, cursor, empty-state text,
   and optional selection predicate/footer text. `GetRowHeight` accounts for the
   font's actual glyph heights and an optional icon-height minimum.

Forward content clicks, drags, drag completion, and wheel input to `ReceiveClick`,
`Drag`, `EndDrag`, and `ReceiveScroll`. The grid owns ascending/descending toggles,
header arrows, paired column resizing, row layout, backgrounds, and the scrollbar.
State belongs to each grid instance; the column count is unrestricted.

Forward `GetContextMenuItems` to `GetHeaderContextMenuItems` before checking row
actions. Right-clicking any visible header returns a checkbox menu for all columns.
At least one column stays visible. Hidden columns keep their preferred widths and
return in their original order; resizing pairs adjacent visible columns. Hiding
the sorted column restores source order. Forward the window's
`ConfigureColumnVisibility` to the grid so the desktop manager restores choices
and saves each toggle in `ModConfig.WindowHiddenColumns`, keyed by farmer and
window ID. Inventory and chest inventory have separate choices; all chests share
the chest window's choices. These survive closing windows and game reloads.
Obsolete column IDs are ignored, new columns start visible, and a config hiding
every column restores the first header so the menu remains accessible.

Also forward `ConfigureColumnWidths` to the grid. The desktop manager restores
preferred widths from `ModConfig.WindowColumnWidths`, keyed by farmer, window ID,
and stable column ID, and saves them once at the end of a divider drag. Widths
survive closing windows and game reloads, including hidden columns. Inventory and
chests have separate settings; all chests share the chest settings. Unknown IDs
are ignored and missing or invalid widths use column defaults. The grid continues
to fit these preferences to the available window width and enforce minimum sizes.

Use `GetRow(cursor)` for selection, context menus, and row actions. It follows the
last drawn layout and ignores partial/empty rows. Use `RowsBounds` for empty-area
and drop-target checks. `Layout` is also available without drawing. Windows own
selection semantics and domain actions; Shift-selection should use `grid.Rows`,
while inventory transfers continue to use source slot order. `Clear` removes the
snapshot, hit boxes, scroll position, and active drag when switching sources,
preserving the selected sort, column widths, and visibility.

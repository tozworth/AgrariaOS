# Lookup Anything desktop integration

AgrariaOS optionally uses the player's installed `Pathoschild.LookupAnything`. LA remains a
separate mod: Agraria does not reference, copy, package, or modify its DLL on disk. The adapter
currently binds to LA 1.55.0 and Stardew 1.6.15/SMAPI 4.5.2. If LA is absent or its internal
members are incompatible, Show Info is omitted and the other Agraria features continue.

Right-click a backpack/chest row to choose **Show Info**. World right-clicks run the game's
normal action first, including Agraria's chest handling. Only when no action is handled does
a target recognized by LA get a **Show Info** popup. Doors, machine collection, chests,
NPCs, animals and item placement keep their direct interactions. Registered world-menu providers
keep their **Choose Option** menus, with **Show Info** added alongside the native choices
when LA recognizes the target. This includes TV, telephone and the bus ticket machine.
The bus uses its resolved map tile; LA normally has no ticket-machine entry, so Show Info
may be absent unless its raw tile information is enabled. Buy-ticket stays available under
the usual driver/repair/horse conditions regardless of LA's installation or tile settings.
The generic popup expires
when the farmer moves, changes held items, changes location/day, or the world viewport moves.

Info uses one desktop window; another lookup or a linked field replaces its page. Drag its
title bar to move it, use the minus button to minimize, and right-click the Pelicom rail for
**Restore [item name]**. The close button or focused Escape closes it. Wheel, Up/Down, and
Page Up/Down scroll the content. The window cannot be resized by the user; it automatically
fits a smaller viewport and saves its position. Location/day changes close stale information.

The adapter creates LA's existing subjects and menu with reflection, preserving LA's data
visibility settings and inventory quality context. A temporary Harmony postfix on LA's own
`OnGameLaunched` handler captures its initialized mod instance; SMAPI's internal registry is
never inspected. The optional dependency ensures LA initializes before Agraria binds it.
Hosted content renders into an individual
texture before the desktop composites it, keeping desktop z-order and clipping. Scoped
Harmony patches replace its background with Agraria's, brighten text/links/table lines, and
prevent its cleanup from restoring global HUD state. Normal F1 lookups keep LA's native
theme and modal behavior. There is no public LA launch/embed API in this revision.

All contextual Show Info entries are composed by `WindowManager.ShowContextMenu` through
`ContextMenuItems.WithInfo`. Desktop windows supply `GetContextMenuInfo`; world providers
supply a native entity or `WorldMenuRequest.LookupTile`. Their actions and lifetime guards
remain intact. Each `IWorldContextMenuProvider` declares `OwnsTarget` so the router gives its
menu priority over the generic Show Info popup. New providers don't need LA-specific patches
or a type allowlist in the integration. UI menus without a lookup target, such as the rail,
remain ordinary menus. This does not turn every vanilla modal dialogue into an Agraria popup.

Before calling the integration visually verified, check Show Info on backpack/chest rows
and world targets without an action; scrolling and linked pages; overlapping windows; moving,
minimize/restore/close; direct right-click on doors, ready machines, chests, NPCs and animals;
Show Info on targets without an action; TV/phone choices and their
Show Info entries (including incoming calls); bus tickets; F1/F8; UI scaling;
save/title and location transitions; and split-screen. Build and headless signature/patch,
color-scope, cleanup and fixed-window checks do not replace these in-game checks.

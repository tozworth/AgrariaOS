# Mail

Mail uses the shared `TreeView` for **All → Inbox / Updates** and `GridView` for the letter list. Inbox contains vanilla collection letters and ordinary pending letters. Updates contains new NPC gift deliveries, recovery bills, cooking letters, recovered items, Winter Star assignments and generated letters without a `Data/mail` key. Every tracked delivery has its own identity; repeated gifts do not replace earlier ones.

`MailArchive` stores JSON in each farmer's `modData` under `Toz.AgrariaOS/MailArchive`. It imports existing collection IDs as read history, observes the current mailbox, and captures live `LetterViewerMenu` instances with a narrow constructor postfix. It continues tracking while F8 shows vanilla. Future mail becomes visible when vanilla delivers it to the mailbox. Non-letter game-state flags are excluded.

Drawing or automatically previewing a selected row has no reward effects. Clicking an unread row with no vanilla menu open processes that delivery through `GameLocation.mailbox()`. Vanilla resolves random gifts, fees, actions, money and recipes; attached quests/orders are accepted as vanilla does on closing the letter. The desktop records the final letter and attachments, removes its temporary viewer, and leaves the remaining mailbox order intact. Already processed deliveries no longer appear at the physical mailbox. Unread deliveries still open the normal vanilla letter UI there, and those reads also enter the archive. Collection viewers do not create deliveries or repeat rewards.

The slim Attachments strip shows icons and quantities. Click **Claim** to transfer an item into the backpack. A full backpack retains the reward; a partial merge retains the remaining quantity. Arrow buttons and wheel scrolling page through multiple attachments. Pending items use Stardew's `Item` XML serializer, preserving quality, upgrades and mod data across saves. Items read through the vanilla viewer remain vanilla's responsibility and are shown as claimed history. Unread envelopes and the Pelicom Mail button overlay emoji **132** from `LooseSprites/emojis`.

Older recovery bills and individual gift deliveries cannot be recovered from collection flags. Imported history has no invented date or item reward; letters with item commands show that previous contents are unavailable. New tracking is saved with the farmer, so returning to an earlier save also restores the earlier archive and reward state.

## Verification

- Build against the installed Stardew 1.6.15 / SMAPI 4.5.2 references.
- Headless checks under `.artifacts/mail-checks/` cover collection imports, pending mail, repeated deliveries, recovery amounts, vanilla reads, JSON reloads, item serialization and partial claims. They use isolated game state and mailbox/inventory fixtures; they do not verify in-game drawing.
- In-game: inspect the tree, resizing, sorting and scrolling; open a static letter, a recovery notice and repeated NPC gifts; compare their text and rewards with vanilla.
- With a full backpack, claim a gift, make room, claim part of a stack, save/reload and finish claiming. Reopening/reclicking must grant no additional items.
- Read another pending letter at the physical mailbox. Confirm vanilla's UI, item handling and collection entry still work, with one archive entry for that delivery. Toggle F8 and repeat.
- Check split-screen/farmhand ownership, quest and recipe letters, recovered items, and paging through multiple tool attachments.

Game startup and visual checks remain user-driven; this change does not launch or install into a running game.

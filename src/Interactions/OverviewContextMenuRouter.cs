using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Objects;

namespace AgrariaOS.Interactions;

// Providers resolve actions for the selected entity; the window owns focus and queue input.
internal sealed record OverviewMenuRequest(GameLocation Location, Farmer Farmer, object? Entity,
    Func<Point> GetTile, Func<bool> IsValid, Func<bool> CanAct, Action<InteractRequest> Queue,
    Func<InteractRequest?> Probe)
{
    internal List<OverviewQueueAction> QueueActions { get; } = new();

    internal ContextMenuItem Action(string text, Func<InteractRequest?> resolve, Func<bool>? available = null)
    {
        bool CanQueue() => IsValid() && CanAct() && (available?.Invoke() ?? true);
        QueueActions.Add(new OverviewQueueAction(text, CanQueue, resolve));
        return new(text, () =>
        {
            if (CanQueue() && resolve() is { } interaction && interaction.Valid())
                Queue(interaction);
        }, CanExecute: CanQueue);
    }
}

internal sealed record OverviewQueueAction(string Text, Func<bool> CanQueue, Func<InteractRequest?> Resolve);

internal interface IOverviewContextMenuProvider
{
    // null lets the next provider (or generic fallback) handle this target.
    ContextMenuItem[]? Create(OverviewMenuRequest request);
}

internal sealed class OverviewContextMenuRouter
{
    private readonly List<IOverviewContextMenuProvider> providers = new();

    internal OverviewContextMenuRouter()
    {
        Register(new NpcOverviewMenuProvider());
        Register(new FarmAnimalOverviewMenuProvider());
        Register(new MachineOverviewMenuProvider());
    }

    internal void Register(IOverviewContextMenuProvider provider) => providers.Add(provider);

    internal ContextMenuItem[] Create(OverviewMenuRequest request)
    {
        request.QueueActions.Clear();
        foreach (IOverviewContextMenuProvider provider in providers)
        {
            if (provider.Create(request) is { } items)
                return items;
        }
        return request.Probe() == null ? Array.Empty<ContextMenuItem>()
            : new[] { request.Action("Interact", request.Probe) };
    }

    internal ContextMenuItem[] CreateMany(IReadOnlyList<OverviewMenuRequest> requests,
        Action<IReadOnlyList<InteractRequest>> queue)
    {
        // Only top-level queued actions participate: immediate Inspect and Gift submenus
        // require a single subject. Mixed selections act on eligible matching rows.
        var actions = new List<OverviewQueueAction>();
        foreach (var request in requests)
        {
            var items = Create(request);
            actions.AddRange(request.QueueActions.Where(action => items.Any(item => item.Text == action.Text
                && item.Children == null)));
        }
        return actions.GroupBy(action => action.Text, StringComparer.Ordinal).Select(group =>
        {
            var matching = group.ToArray();
            return new ContextMenuItem(group.Key, () =>
            {
                var interactions = new List<InteractRequest>();
                foreach (var action in matching)
                {
                    if (action.CanQueue() && action.Resolve() is { } interaction && interaction.Valid())
                        interactions.Add(interaction);
                }
                if (interactions.Count > 0)
                    queue(interactions);
            }, CanExecute: () => matching.Any(action => action.CanQueue()));
        }).ToArray();
    }
}

internal sealed class NpcOverviewMenuProvider : IOverviewContextMenuProvider
{
    public ContextMenuItem[]? Create(OverviewMenuRequest request)
    {
        if (request.Entity is not NPC npc || npc.IsMonster || npc is Pet or Horse)
            return null;

        bool Present() => request.IsValid() && request.Location.characters.Contains(npc)
            && !npc.IsInvisible && !npc.isSleeping.Value;
        InteractRequest ForNpc(Func<bool> eligible, Action<Farmer> perform)
        {
            bool Valid() => Present() && eligible();
            Point Tile() => npc.TilePoint;
            Rectangle Bounds()
            {
                Point tile = Tile();
                return new Rectangle(tile.X, tile.Y, 1, 1);
            }
            // LiveTiles keeps the selected callback through chase replans, including gifting.
            return new InteractRequest(new ApproachTarget(Bounds(), Valid, Bounds), Tile, Valid, who =>
            {
                Point tile = Tile();
                if (who == request.Farmer && Valid() && Utility.tileWithinRadiusOfPlayer(tile.X, tile.Y, 1, who))
                    perform(who);
            });
        }

        var gifts = NpcGiftActions.GetOwnedGifts(npc, request.Farmer);
        var children = new List<ContextMenuItem>();
        foreach (int taste in new[] { NPC.gift_taste_love, NPC.gift_taste_like })
        {
            children.Add(new ContextMenuItem(taste == NPC.gift_taste_love ? "Loved" : "Liked", IsEnabled: false));
            var group = gifts.Where(gift => gift.Taste == taste).ToArray();
            if (group.Length == 0)
                children.Add(new ContextMenuItem("None in backpack", IsEnabled: false));
            foreach (var gift in group)
            {
                bool Eligible() => NpcGiftActions.CanGive(npc, request.Farmer, gift.Item);
                children.Add(request.Action(NpcGiftActions.GetLabel(gift.Item),
                    () => ForNpc(Eligible, who => NpcGiftActions.Give(npc, who, gift.Item)),
                    () => Present() && Eligible()));
            }
        }
        return new[]
        {
            request.Action("Talk", () => ForNpc(() => true, who => NpcGiftActions.Talk(npc, who, request.Location))
                with { Identity = npc },
                () => Present() && request.Farmer.TemporaryItem == null),
            new ContextMenuItem("Gift", Children: children.ToArray(),
                CanExecute: () => Present() && request.CanAct()
                    && gifts.Any(gift => NpcGiftActions.CanGive(npc, request.Farmer, gift.Item)))
        };
    }
}

internal sealed class FarmAnimalOverviewMenuProvider : IOverviewContextMenuProvider
{
    public ContextMenuItem[]? Create(OverviewMenuRequest request)
    {
        if (request.Entity is not FarmAnimal animal)
            return null;
        bool Present() => request.IsValid()
            && request.Location.animals.Values.Any(live => ReferenceEquals(live, animal));
        return new[]
        {
            new ContextMenuItem("Inspect", () =>
            {
                if (Present() && request.CanAct())
                    Game1.activeClickableMenu = new AnimalQueryMenu(animal);
            }, CanExecute: () => Present() && request.CanAct()),
            request.Action("Pet", () => InteractProbe.TryResolvePetting(request.Location, request.Farmer, animal),
                () => Present() && InteractProbe.CanPet(request.Location, request.Farmer, animal))
        };
    }
}

internal sealed class MachineOverviewMenuProvider : IOverviewContextMenuProvider
{
    public ContextMenuItem[]? Create(OverviewMenuRequest request)
    {
        if (request.Entity is not StardewValley.Object machine || machine is Chest or Furniture)
            return null;
        InteractRequest? Resolve()
            => InteractProbe.TryResolveMachineOutput(request.Location, request.Farmer, request.GetTile());
        if (Resolve() == null)
            return null;
        return new[]
        {
            request.Action("Collect", Resolve, () => !machine.isTemporarilyInvisible
                && machine.readyForHarvest.Value && machine.heldObject.Value != null)
        };
    }
}

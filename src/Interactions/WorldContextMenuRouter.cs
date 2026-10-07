using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace AgrariaOS.Interactions;

// Native callers supply their complete, localized choices and original continuation before
// creating a modal dialogue. Providers only decide which shortcuts to expose.
internal sealed record WorldMenuRequest(GameLocation Location, object Target,
    IReadOnlyList<Response> Choices, Action<Response> Choose, Vector2? LookupTile = null);

internal sealed record WorldMenuTarget(GameLocation Location, object? Entity, Vector2 Tile);

internal sealed record WorldMenu(ContextMenuItem[] Items, Func<bool> IsValid);

internal interface IWorldContextMenuProvider
{
    // Claim the target before generic actions are offered. Vanilla still resolves reach,
    // eligibility and the localized choices; claiming a target never executes its action.
    bool OwnsTarget(WorldMenuTarget target);
    WorldMenu? Create(WorldMenuRequest request);
}

internal sealed class WorldContextMenuRouter
{
    private readonly List<IWorldContextMenuProvider> providers = new();

    internal WorldContextMenuRouter()
    {
        Register(new TelevisionContextMenuProvider());
        Register(new TelephoneContextMenuProvider());
        Register(new BusTicketContextMenuProvider());
    }

    // Add providers here without changing popup rendering or input ownership.
    internal void Register(IWorldContextMenuProvider provider) => providers.Add(provider);

    internal bool OwnsTarget(WorldMenuTarget target) => providers.Any(provider => provider.OwnsTarget(target));

    internal WorldMenu? Create(WorldMenuRequest request)
    {
        foreach (IWorldContextMenuProvider provider in providers)
        {
            if (provider.Create(request) is { Items.Length: > 0 } menu)
                return menu;
        }
        return null;
    }
}

internal sealed class TelevisionContextMenuProvider : IWorldContextMenuProvider
{
    public bool OwnsTarget(WorldMenuTarget target) => target.Entity is TV;

    public WorldMenu? Create(WorldMenuRequest request)
    {
        if (request.Target is not TV tv)
            return null;
        ContextMenuItem[] items = request.Choices.Where(choice => choice.responseKey != "(Leave)")
            .Select(choice => new ContextMenuItem(choice.responseText, () => request.Choose(choice))).ToArray();
        return new WorldMenu(items, () => request.Location.furniture.Contains(tv));
    }
}

internal sealed class TelephoneContextMenuProvider : IWorldContextMenuProvider
{
    public bool OwnsTarget(WorldMenuTarget target) => target.Entity is Phone;

    public WorldMenu? Create(WorldMenuRequest request)
    {
        if (request.Target is not Phone phone)
            return null;
        var items = request.Choices.Where(choice => choice.responseKey != "HangUp")
            .Select(choice => new ContextMenuItem(choice.responseText, () =>
            {
                Phone.StopRinging();
                request.Choose(choice);
            })).ToList();
        // Right-click still exposes the directory during an incoming call. Keep an explicit
        // answer action too; opening or dismissing the directory doesn't discard that call.
        if (Phone.whichPhoneCall is { } incomingCall)
        {
            items.Insert(0, new ContextMenuItem("Answer-call", () =>
            {
                if (Phone.whichPhoneCall == incomingCall)
                    phone.checkForAction(Game1.player);
            }));
        }
        return new WorldMenu(items.ToArray(), () => request.Location.Objects.TryGetValue(phone.TileLocation, out var placed)
            && ReferenceEquals(placed, phone));
    }
}

internal sealed class BusTicketContextMenuProvider : IWorldContextMenuProvider
{
    public bool OwnsTarget(WorldMenuTarget target) => target.Location is BusStop bus
        && bus.getTileIndexAt((int)target.Tile.X, (int)target.Tile.Y, "Buildings", "outdoors") == 1057;

    internal static bool HasDriver(BusStop bus)
    {
        // Vanilla requires Pam at this exact driving spot, not merely somewhere on the map.
        return bus.characters.Any(character => character.Name == "Pam" && character.TilePoint == new Point(21, 10));
    }

    public WorldMenu? Create(WorldMenuRequest request)
    {
        if (request.Target is not BusStop bus || !HasDriver(bus))
            return null;
        Response? yes = request.Choices.FirstOrDefault(choice => choice.responseKey == "Yes");
        if (yes == null)
            return null;
        int price = bus.TicketPrice;
        return new WorldMenu(new[]
        {
            new ContextMenuItem($"Buy-ticket ({price}g)", () => request.Choose(yes))
        }, () => HasDriver(bus) && bus.TicketPrice == price);
    }
}

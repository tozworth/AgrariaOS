using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

/// <summary>A single pending leg. Tool legs carry a ToolTarget, approach legs a plain
/// ApproachTarget, interact legs an InteractRequest, and plain moves neither.</summary>
internal sealed record QueuedStep(
    Point Clicked,
    ApproachTarget? Target,
    InteractRequest? Request,
    Point? Warp,
    GameLocation Location,
    uint Day)
{
    internal Point? PlannedTile { get; init; }
}

internal readonly record struct QueueIndicator(Point Tile, Color Color, Character? Animal);

internal sealed partial class ClickToMove
{
    internal const int MaxQueued = 9;
    private const int MaxSwingsPerTool = 30;

    private readonly Func<bool> extendedQueue;

    internal ClickToMove(Func<bool>? extendedQueue = null)
        => this.extendedQueue = extendedQueue ?? (() => false);

    private Farmer? farmer;
    private GameLocation? location;
    private PathFindController? controller;
    private ApproachTarget? target;
    private Point? warpDestination;
    private Point destination;
    private Point routeEnd;
    private Point? plannedInteractionTile;
    private uint day;
    private bool arrived;
    private Vector2 previousPosition;
    private double stalledMilliseconds;
    private int replans;
    internal string LastDebug { get; private set; } = "";
    private Farmer? toolFarmer;
    private Tool? startedTool;
    private int releaseTicks;
    private Func<Point>? interactTile;
    private Func<bool>? interactValid;
    private Action<Farmer>? interactPerform;
    private InteractRequest? interactRequest;
    private Point? arrivedInteractTile;
    private Action<Farmer>? arrivedInteractPerform;
    private InteractRequest? arrivedInteractRequest;
    private InteractRequest? toolRequest;
    private ToolTarget? repeatTool;
    private int repeatSwings;
    private readonly Queue<QueuedStep> pending = new();

    internal Point? Destination => controller != null || arrived ? ActiveDisplayTile() ?? destination : null;

    internal bool IsBusy => controller != null || arrived || repeatTool != null || toolFarmer != null
        || arrivedInteractTile != null || pending.Count > 0;

    internal int TotalCount => pending.Count
        + (controller != null || arrived || repeatTool != null || toolFarmer != null || arrivedInteractTile != null ? 1 : 0);

    internal bool HasQueuedTool(ToolTarget tool) => target is ToolTarget active && SameTool(active, tool)
        || repeatTool != null && SameTool(repeatTool, tool)
        || pending.Any(step => step.Target is ToolTarget queued && SameTool(queued, tool));

    private static bool SameTool(ToolTarget a, ToolTarget b) => a.Tiles == b.Tiles && a.IsValid() && b.IsValid();


    internal bool HasQueuedInteraction(Point tile) => arrivedInteractTile == tile
        || interactTile?.Invoke() == tile || pending.Any(step => step.Request?.Tile() == tile);

    internal bool HasQueuedRequest(InteractRequest request) => request.Identity == null
        ? HasQueuedInteraction(request.Tile())
        : SameRequest(interactRequest, request) || SameRequest(arrivedInteractRequest, request)
            || SameRequest(toolRequest, request) || pending.Any(step => SameRequest(step.Request, request));

    private static bool SameRequest(InteractRequest? a, InteractRequest b)
        => a != null && a.Kind == b.Kind && ReferenceEquals(a.Identity, b.Identity);




























}

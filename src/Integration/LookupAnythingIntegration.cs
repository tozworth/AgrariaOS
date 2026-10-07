using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace AgrariaOS.Integration;

// No LA reference or copied implementation: all subjects, fields and menus come from the
// installed mod. Bind once, and restrict presentation patches to our hosted menu instances.
internal sealed class LookupAnythingIntegration
{
    internal const string ModId = "Pathoschild.LookupAnything";
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    internal Harmony Harmony { get; }
    internal bool Available { get; private set; }
    private bool prepared;
    private MethodInfo? captureMethod;
    private static LookupAnythingIntegration? awaitingInstance;
    private Mod mod = null!;
    private object factory = null!;
    private object itemProvider = null!;
    private object inventoryContext = null!;
    private ConstructorInfo menuConstructor = null!;
    private MethodInfo buildItem = null!;
    private MethodInfo worldSubject = null!;
    private MethodInfo entitySubject = null!;
    private MethodInfo tileTarget = null!;
    private PropertyInfo targetSubject = null!;
    private FieldInfo configField = null!;
    private FieldInfo themeField = null!;
    private FieldInfo scrollUp = null!;
    private FieldInfo scrollDown = null!;
    private FieldInfo validatedDraw = null!;
    private MethodInfo scrollUpMethod = null!;
    private MethodInfo scrollDownMethod = null!;
    private PropertyInfo subjectName = null!;
    private MethodInfo subjectPortrait = null!;
    private Type tileSubjectType = null!;
    private FieldInfo tileLocation = null!;
    private FieldInfo tilePosition = null!;
    private static readonly ConditionalWeakTable<IClickableMenu, LookupWindow> Hosts = new();
    [ThreadStatic] private static IClickableMenu? renderingMenu;

    internal LookupAnythingIntegration(IModHelper helper, IMonitor monitor, string ownerId)
    {
        this.helper = helper;
        this.monitor = monitor;
        Harmony = new Harmony(ownerId + ".LookupAnything");
    }

    internal void Prepare()
    {
        if (!helper.ModRegistry.IsLoaded(ModId))
            return;
        // The optional dependency loads LA before our Entry, but GameLaunched hasn't
        // fired yet. Capture LA's own instance after its handler initializes the factory;
        // never inspect SMAPI's private registry or use its reflection API on internals.
        Type entryType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Pathoschild.Stardew.LookupAnything.ModEntry"))
            .OfType<Type>().Single();
        captureMethod = RequireMethod(entryType, "OnGameLaunched");
        awaitingInstance = this;
        Harmony.Patch(captureMethod, postfix: new HarmonyMethod(typeof(LookupAnythingIntegration), nameof(CaptureInstancePostfix)));
        prepared = true;
    }

    private static void CaptureInstancePostfix(Mod __instance)
    {
        if (awaitingInstance != null)
            awaitingInstance.mod = __instance;
    }

    internal void Initialize()
    {
        if (!prepared)
            return;
        if (mod == null)
            throw new InvalidOperationException("Lookup Anything's GameLaunched handler hasn't supplied its instance.");
        Assembly assembly = mod.GetType().Assembly;
        Type menuType = RequireType(assembly, "Pathoschild.Stardew.LookupAnything.Components.LookupMenu");
        menuConstructor = menuType.GetConstructors().Single(ctor => ctor.GetParameters().Length == 8);
        Type subjectType = menuConstructor.GetParameters()[0].ParameterType;
        subjectName = subjectType.GetProperty("Name") ?? throw new MissingMemberException(subjectType.FullName, "Name");
        subjectPortrait = RequireMethod(subjectType, "DrawPortrait");
        tileSubjectType = RequireType(assembly, "Pathoschild.Stardew.LookupAnything.Framework.Lookups.Tiles.TileSubject");
        tileLocation = RequireField(tileSubjectType, "Location");
        tilePosition = RequireField(tileSubjectType, "Position");
        configField = RequireField(mod.GetType(), "Config");
        themeField = RequireField(mod.GetType(), "Theme");
        factory = RequireField(mod.GetType(), "TargetFactory").GetValue(mod)
            ?? throw new InvalidOperationException("Lookup Anything hasn't initialized its target factory.");
        itemProvider = ((IEnumerable)RequireField(factory.GetType(), "LookupProviders").GetValue(factory)!)
            .Cast<object>().Single(provider => provider.GetType().Name == "ItemLookupProvider");
        buildItem = itemProvider.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "BuildSubject" && method.GetParameters()[0].ParameterType == typeof(Item));
        inventoryContext = Enum.Parse(buildItem.GetParameters()[1].ParameterType, "Inventory");
        worldSubject = factory.GetType().GetMethods().Single(method => method.Name == "GetSubjectFrom"
            && method.GetParameters().Length == 3 && method.GetParameters()[0].ParameterType == typeof(Farmer));
        entitySubject = RequireMethod(factory.GetType(), "GetByEntity");
        tileTarget = RequireMethod(factory.GetType(), "GetTargetFromTile");
        targetSubject = RequireType(assembly, "Pathoschild.Stardew.LookupAnything.Framework.Lookups.ITarget")
            .GetProperty("GetSubject") ?? throw new MissingMemberException("Lookup target has no subject factory.");
        scrollUp = RequireField(menuType, "ScrollUpButton");
        scrollDown = RequireField(menuType, "ScrollDownButton");
        validatedDraw = RequireField(menuType, "ValidatedDrawMode");
        scrollUpMethod = RequireMethod(menuType, "ScrollUp");
        scrollDownMethod = RequireMethod(menuType, "ScrollDown");
        InstallPresentationPatches(assembly, menuType);
        Harmony.Unpatch(captureMethod!, HarmonyPatchType.Postfix, Harmony.Id);
        awaitingInstance = null;
        Available = true;
        monitor.Log($"Lookup Anything {mod.ModManifest.Version}: desktop Show Info ready.", LogLevel.Info);
    }

    internal void Disable(Exception error)
    {
        Available = false;
        prepared = false;
        if (ReferenceEquals(awaitingInstance, this))
            awaitingInstance = null;
        Harmony.UnpatchAll(Harmony.Id);
        monitor.Log($"Lookup Anything desktop integration disabled; ordinary LA remains available.\n{error}", LogLevel.Warn);
    }

    internal object? GetInventorySubject(Item item) => GetSubject(() =>
        buildItem.Invoke(itemProvider, new object?[] { item, inventoryContext, null, true }));

    internal object? GetWorldSubject() => GetSubject(() =>
        worldSubject.Invoke(factory, new object[] { Game1.player, Game1.currentLocation, true }));

    internal object? GetSubjectEntity(object subject) => AccessTools.Field(subject.GetType(), "Target")?.GetValue(subject);

    internal object? GetEntitySubject(object target, GameLocation location) => GetSubject(() =>
        entitySubject.Invoke(factory, new object[] { target, location }));

    internal object? GetTileSubject(GameLocation location, Vector2 tile) => GetSubject(() =>
    {
        object? target = tileTarget.Invoke(factory, new object[] { location, tile });
        return target == null ? null : ((Delegate)targetSubject.GetValue(target)!).DynamicInvoke();
    });

    private object? GetSubject(Func<object?> get)
    {
        if (!Available)
            return null;
        try { return get(); }
        catch (Exception ex)
        {
            monitor.Log($"Couldn't get Lookup Anything information.\n{ex}", LogLevel.Warn);
            return null;
        }
    }

    internal string GetName(object subject) => subjectName.GetValue(subject)?.ToString() ?? "Info";

    internal bool DrawSubjectIcon(object subject, SpriteBatch batch, int size)
    {
        object? entity = GetSubjectEntity(subject);
        // Character portraits can scale by width alone; fit the actual sprite in both axes.
        if (entity is Character { Sprite: { } sprite })
        {
            DrawFittedSprite(batch, sprite.Texture, sprite.SourceRect, size, Color.White);
            return true;
        }
        // Tree.drawInMenu uses world-sized offsets which put its portrait outside the slot.
        if (entity is Tree tree && tree.texture.Value is { } treeTexture)
        {
            Rectangle source = tree.growthStage.Value switch
            {
                0 => new Rectangle(32, 128, 16, 16),
                1 => new Rectangle(0, 128, 16, 16),
                2 => new Rectangle(16, 128, 16, 16),
                < 5 => new Rectangle(0, 96, 16, 32),
                _ => tree.stump.Value ? Tree.stumpSourceRect : Tree.treeTopSourceRect
            };
            DrawFittedSprite(batch, treeTexture, source, size,
                tree.growthStage.Value < 5 && tree.fertilized.Value ? Color.HotPink : Color.White,
                tree.flipped.Value ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            return true;
        }
        if ((bool)subjectPortrait.Invoke(subject, new object[] { batch, Vector2.Zero, new Vector2(size) })!)
            return true;

        // LA has no portrait for map tiles. Composite the inspected tile's own map layers.
        if (!tileSubjectType.IsInstanceOfType(subject))
            return false;
        var location = (GameLocation)tileLocation.GetValue(subject)!;
        var tile = (Vector2)tilePosition.GetValue(subject)!;
        bool drawn = false;
        foreach (var layer in location.Map.Layers)
        {
            int x = (int)tile.X, y = (int)tile.Y;
            if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight || layer.Tiles[x, y] is not { } mapTile)
                continue;
            var image = mapTile.TileSheet.GetTileImageBounds(mapTile.TileIndex);
            Texture2D texture = Game1.content.Load<Texture2D>(mapTile.TileSheet.ImageSource);
            batch.Draw(texture, new Rectangle(0, 0, size, size),
                new Rectangle(image.X, image.Y, image.Width, image.Height), Color.White);
            drawn = true;
        }
        return drawn;
    }

    private static void DrawFittedSprite(SpriteBatch batch, Texture2D texture, Rectangle source, int size,
        Color color, SpriteEffects effects = SpriteEffects.None)
    {
        float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
        Vector2 position = new((size - source.Width * scale) / 2, (size - source.Height * scale) / 2);
        batch.Draw(texture, position, source, color, 0, Vector2.Zero, scale, effects, 0);
    }

    internal IClickableMenu CreateMenu(object subject, LookupWindow host)
    {
        object config = configField.GetValue(mod)!;
        Delegate navigation = Delegate.CreateDelegate(menuConstructor.GetParameters()[7].ParameterType, host,
            typeof(LookupWindow).GetMethod(nameof(LookupWindow.ShowPage), BindingFlags.Instance | BindingFlags.NonPublic)!);
        bool hud = Game1.displayHUD;
        IClickableMenu menu;
        try
        {
            menu = (IClickableMenu)menuConstructor.Invoke(new[]
            {
                subject, mod.Monitor, mod.Helper.Reflection, themeField.GetValue(mod),
                config.GetType().GetProperty("ScrollAmount")!.GetValue(config),
                config.GetType().GetProperty("ShowDataMiningFields")!.GetValue(config), false, navigation
            });
        }
        finally
        {
            // LA's constructor briefly sets this field; no game tick/render runs inside
            // construction. Restore even on failure, without opening a vanilla modal.
            Game1.displayHUD = hud;
        }
        Hosts.Add(menu, host);
        validatedDraw.SetValue(menu, true); // our own deferred render pass is known to be compatible
        menu.exitFunction = host.RequestClose;
        return menu;
    }

    internal void DisposeMenu(IClickableMenu menu)
    {
        try { ((IDisposable)menu).Dispose(); }
        finally { Hosts.Remove(menu); }
    }

    internal void LayoutMenu(IClickableMenu menu, Point size)
    {
        menu.xPositionOnScreen = menu.yPositionOnScreen = 0;
        menu.width = size.X;
        menu.height = size.Y;
        var up = (ClickableTextureComponent)scrollUp.GetValue(menu)!;
        var down = (ClickableTextureComponent)scrollDown.GetValue(menu)!;
        down.bounds = new Rectangle(15, size.Y - 30 - down.sourceRect.Height, down.sourceRect.Width, down.sourceRect.Height);
        up.bounds = new Rectangle(15, down.bounds.Y - 15 - up.sourceRect.Height, up.sourceRect.Width, up.sourceRect.Height);
        menu.upperRightCloseButton.visible = false;
        menu.upperRightCloseButton.bounds = Rectangle.Empty;
    }

    internal void Scroll(IClickableMenu menu, bool up, int? amount = null)
        => (up ? scrollUpMethod : scrollDownMethod).Invoke(menu, new object?[] { amount });

    internal void DrawMenu(IClickableMenu menu, SpriteBatch overlay)
    {
        IClickableMenu? previous = renderingMenu;
        renderingMenu = menu;
        try { menu.draw(overlay); }
        finally { renderingMenu = previous; }
    }

    internal void ReportFailure(Exception error)
        => monitor.Log($"Lookup Anything window failed.\n{error}", LogLevel.Warn);

    private void InstallPresentationPatches(Assembly assembly, Type menuType)
    {
        Patch(RequireMethod(menuType, "CleanupImpl"), nameof(CleanupPrefix));
        Patch(RequireMethod(menuType, "UpdateLayout"), nameof(LayoutPrefix));
        Patch(RequireMethod(RequireType(assembly, "Pathoschild.Stardew.LookupAnything.Framework.Themes.MenuBackground"), "Draw"),
            nameof(BackgroundPrefix));
        Patch(AccessTools.DeclaredMethod(typeof(IClickableMenu), nameof(IClickableMenu.drawMouse)), nameof(MousePrefix));

        Type common = RequireType(assembly, "Pathoschild.Stardew.Common.CommonHelper");
        Type text = assembly.GetType("Pathoschild.Stardew.LookupAnything.DrawTextHelper")
            ?? RequireType(assembly, "Pathoschild.Stardew.LookupAnything.DrawHelper");
        foreach (Type type in new[] { common, text })
        {
            MethodInfo drawText = type.GetMethods().Single(method => method.Name == "DrawTextBlock"
                && method.GetParameters()[2].ParameterType == typeof(string));
            Patch(drawText, nameof(TextPrefix));
        }
        Type formatted = RequireType(assembly, "Pathoschild.Stardew.LookupAnything.Framework.FormattedText");
        Harmony.Patch(formatted.GetProperty("Color")!.GetMethod!, postfix: new HarmonyMethod(typeof(LookupAnythingIntegration), nameof(FormattedColorPostfix)));
        foreach (Type type in new[] { common, RequireType(assembly, "Pathoschild.Stardew.Common.DrawHelper") }.Distinct())
            Patch(RequireMethod(type, "DrawLine"), nameof(LinePrefix));
    }

    private void Patch(MethodInfo method, string prefix)
        => Harmony.Patch(method, prefix: new HarmonyMethod(typeof(LookupAnythingIntegration), prefix));

    private static bool CleanupPrefix(IClickableMenu __instance) => !Hosts.TryGetValue(__instance, out _);
    private static bool LayoutPrefix(IClickableMenu __instance) => !Hosts.TryGetValue(__instance, out _);
    private static bool BackgroundPrefix() => renderingMenu == null;
    private static bool MousePrefix(IClickableMenu __instance) => !ReferenceEquals(renderingMenu, __instance);
    private static void TextPrefix(ref Color? color)
    {
        if (renderingMenu != null)
            color = LightColor(color ?? Color.Black);
    }
    private static void FormattedColorPostfix(ref Color? __result)
    {
        if (renderingMenu != null)
            __result = LightColor(__result ?? Color.Black);
    }
    private static void LinePrefix(ref Color? color)
    {
        if (renderingMenu != null && color is { } value)
            color = LightColor(value);
    }

    private static Color LightColor(Color color)
    {
        // Preserve alpha and semantic accents, including faded/undiscovered entries.
        Color rgb = new(color.R, color.G, color.B);
        bool Matches(Color named)
        {
            Color faded = named * (color.A / 255f);
            return rgb == named || (Math.Abs(color.R - faded.R) <= 1
                && Math.Abs(color.G - faded.G) <= 1 && Math.Abs(color.B - faded.B) <= 1);
        }
        Color replacement = Matches(Color.Black) ? UiStyle.Primary
            : Matches(Color.Gray) ? UiStyle.Muted
            : Matches(Color.Blue) ? new Color(104, 214, 255)
            : Matches(Color.Green) ? new Color(137, 221, 151)
            : Matches(Color.Red) ? new Color(255, 127, 127) : rgb;
        replacement.A = color.A;
        return replacement;
    }

    private static Type RequireType(Assembly assembly, string name)
        => assembly.GetType(name, throwOnError: true)!;
    private static FieldInfo RequireField(Type type, string name)
        => AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
    private static MethodInfo RequireMethod(Type type, string name)
        => AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);
}

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
internal sealed partial class LookupAnythingIntegration
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















    internal void ReportFailure(Exception error)
        => monitor.Log($"Lookup Anything window failed.\n{error}", LogLevel.Warn);





}

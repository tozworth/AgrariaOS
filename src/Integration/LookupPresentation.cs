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

partial class LookupAnythingIntegration
{
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

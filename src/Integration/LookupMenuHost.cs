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

    private static bool CleanupPrefix(IClickableMenu __instance) => !Hosts.TryGetValue(__instance, out _);

    private static bool LayoutPrefix(IClickableMenu __instance) => !Hosts.TryGetValue(__instance, out _);

    private static bool BackgroundPrefix() => renderingMenu == null;

    private static bool MousePrefix(IClickableMenu __instance) => !ReferenceEquals(renderingMenu, __instance);

}

using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

internal sealed class LookupWindow : OsWindow
{
    internal const string WindowId = "LookupInfo";
    private readonly LookupAnythingIntegration integration;
    private readonly Farmer farmer = Game1.player;
    private readonly GameLocation location = Game1.currentLocation;
    private readonly uint day = Game1.stats.DaysPlayed;
    private IClickableMenu? menu;
    private RenderTarget2D? target;
    private RenderTarget2D? iconTarget;
    private object? subject;
    private bool iconDirty;
    private bool hasIcon;
    private SpriteBatch? overlay;
    private Rectangle contentBounds;
    private string name = "Info";
    private bool closed;
    private string? error;

    internal LookupWindow(LookupAnythingIntegration integration) => this.integration = integration;
    internal override string Id => WindowId;
    internal override string Title => name;
    internal override string TitleSubtext => "Info";
    internal override int Icon => UiStyle.NotificationIcon;
    internal override bool CanResize => false;
    internal override Point DefaultSize => new(960, 600);
    protected override bool DrawContentBackground => false; // rendered content supplies its own single opacity layer
    internal override bool IsValid => !closed && Context.IsWorldReady && Game1.player == farmer
        && Game1.currentLocation == location && Game1.stats.DaysPlayed == day;

    internal void ShowPage(object subject)
    {
        try
        {
            IClickableMenu next = integration.CreateMenu(subject, this);
            if (menu != null)
                integration.DisposeMenu(menu);
            menu = next;
            name = integration.GetName(subject);
            this.subject = subject;
            iconDirty = true;
            hasIcon = false;
            closed = false;
            error = null;
            contentBounds = Rectangle.Empty; // require a new draw before using link geometry
        }
        catch (Exception ex) { Fail(ex); }
    }

    internal void RequestClose() => closed = true;

    internal override void Update()
    {
        if (menu != null && IsValid && !IsMinimized && error == null)
        {
            try { menu.update(Game1.currentGameTime); }
            catch (Exception ex) { Fail(ex); }
        }
    }

    internal override void Close()
    {
        closed = true;
        if (menu != null)
            integration.DisposeMenu(menu);
        menu = null;
        target?.Dispose();
        target = null;
        iconTarget?.Dispose();
        iconTarget = null;
        subject = null;
        overlay?.Dispose();
        overlay = null;
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (leftClick && menu != null && error == null && contentBounds.Contains(cursor))
        {
            try { menu.receiveLeftClick(cursor.X - contentBounds.X, cursor.Y - contentBounds.Y); }
            catch (Exception ex) { Fail(ex); }
        }
    }

    internal override void ReceiveScroll(int delta)
    {
        if (menu != null && error == null)
        {
            try { menu.receiveScrollWheelAction(delta); }
            catch (Exception ex) { Fail(ex); }
        }
    }

    internal override bool ReceiveShortcut(SButton button)
    {
        if (button is SButton.Escape or SButton.ControllerB)
        {
            RequestClose();
            return true;
        }
        bool up = button is SButton.Up or SButton.PageUp or SButton.RightThumbstickUp;
        bool down = button is SButton.Down or SButton.PageDown or SButton.RightThumbstickDown;
        if ((!up && !down) || menu == null || error != null)
            return false;
        try { integration.Scroll(menu, up, button is SButton.PageUp or SButton.PageDown ? menu.height : null); }
        catch (Exception ex) { Fail(ex); }
        return true;
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle bounds, ModConfig config)
    {
        contentBounds = bounds;
        if (menu != null && error == null)
        {
            try
            {
                Render(bounds.Size);
                batch.Draw(target!, bounds, Color.White * config.Opacity);
            }
            catch (Exception ex) { Fail(ex); }
        }
        if (error != null)
        {
            batch.Draw(Game1.staminaRect, bounds, UiStyle.Background * config.Opacity);
            UiText.Draw(batch, error, new Vector2(bounds.X + 15, bounds.Y + 15), UiStyle.Primary, config);
        }
    }

    internal override void DrawTitleIcon(SpriteBatch batch, Point position, ModConfig config)
    {
        if (iconDirty && subject != null)
        {
            iconDirty = false;
            try
            {
                // Item/terrain portraits assume a 64px menu slot even when given a smaller size.
                // Some LA portraits build a texture in a nested render pass before drawing.
                iconTarget ??= new RenderTarget2D(Game1.graphics.GraphicsDevice, 64, 64, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                RenderInto(iconTarget, Color.Transparent, () => hasIcon = integration.DrawSubjectIcon(subject, overlay!, 64));
            }
            catch (Exception ex)
            {
                hasIcon = false;
                integration.ReportFailure(ex);
            }
        }
        if (hasIcon)
            batch.Draw(iconTarget!, new Rectangle(position.X, position.Y, 18, 18), Color.White * config.Opacity);
        else
            base.DrawTitleIcon(batch, position, config);
    }

    private void Render(Point size)
    {
        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        if (target == null || target.IsDisposed || target.Width != size.X || target.Height != size.Y)
        {
            target?.Dispose();
            target = new RenderTarget2D(device, size.X, size.Y, false, SurfaceFormat.Color, DepthFormat.None);
        }
        integration.LayoutMenu(menu!, size);
        // LA's content blend preserves destination alpha, so establish an opaque base.
        RenderInto(target, UiStyle.Background, () => integration.DrawMenu(menu!, overlay!));
    }

    private void RenderInto(RenderTarget2D destination, Color background, Action draw)
    {
        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        overlay ??= new SpriteBatch(device);

        // The game's HUD batch is deferred, so none of its queued draws are redirected.
        // LA's separate batches flush into this target; then our texture participates in
        // ordinary desktop z-order. Restore all device state before returning to the HUD.
        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport viewport = device.Viewport;
        Rectangle scissor = device.ScissorRectangle;
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState rasterizer = device.RasterizerState;
        SamplerState sampler = device.SamplerStates[0];
        bool begun = false;
        try
        {
            device.SetRenderTarget(destination);
            device.Clear(background);
            overlay.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            begun = true;
            draw();
            overlay.End();
            begun = false;
        }
        finally
        {
            try
            {
                if (begun)
                    overlay.End();
            }
            finally
            {
                device.SetRenderTargets(previousTargets);
                device.Viewport = viewport;
                device.ScissorRectangle = scissor;
                device.BlendState = blend;
                device.DepthStencilState = depth;
                device.RasterizerState = rasterizer;
                device.SamplerStates[0] = sampler;
            }
        }
    }

    private void Fail(Exception ex)
    {
        if (error == null)
            integration.ReportFailure(ex);
        error = "Lookup failed. See the SMAPI log; close and try again.";
    }
}

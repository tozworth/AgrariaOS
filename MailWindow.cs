using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgrariaOS;

internal sealed class MailWindow : OsWindow
{
    internal const string WindowId = "Mail";
    internal override string Id => WindowId;
    internal override string Title => "Mail";
    internal override int Icon => PelicomStyle.MailIcon;

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
        => DrawCenteredText(batch, "No mail.", contentBounds, PelicomStyle.Muted, config);
}

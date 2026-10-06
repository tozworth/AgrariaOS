using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgrariaOS.Desktop.Windows;

internal sealed class MailWindow : OsWindow
{
    internal const string WindowId = "Mail";
    internal override string Id => WindowId;
    internal override string Title => "Mail";
    internal override int Icon => UiStyle.MailIcon;

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
        => DrawCenteredText(batch, "No mail.", contentBounds, UiStyle.Muted, config);
}

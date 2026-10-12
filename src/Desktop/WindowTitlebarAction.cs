using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgrariaOS.Desktop;

// DrawIcon receives the button bounds and an opacity-adjusted foreground color.
// Windows can supply any icon renderer; chrome owns layout, hover and click priority.
internal sealed record WindowTitlebarAction(
    Action<SpriteBatch, Rectangle, Color> DrawIcon,
    Action OnClick,
    Func<bool>? CanExecute = null)
{
    internal bool IsEnabled => CanExecute?.Invoke() ?? true;

    internal void Invoke()
    {
        if (IsEnabled)
            OnClick();
    }
}

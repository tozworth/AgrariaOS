using Microsoft.Xna.Framework.Input;

namespace AgrariaOS.Desktop;

// The native cursor belongs to the game window, rather than any one local screen.
internal sealed class HardwareCursor
{
    private MouseCursor? appliedCursor;

    internal void Update(WindowResizeEdges edges)
    {
        MouseCursor? cursor = edges switch
        {
            WindowResizeEdges.Left or WindowResizeEdges.Right => MouseCursor.SizeWE,
            WindowResizeEdges.Top or WindowResizeEdges.Bottom => MouseCursor.SizeNS,
            WindowResizeEdges.Left | WindowResizeEdges.Top => MouseCursor.SizeNWSE,
            WindowResizeEdges.Right | WindowResizeEdges.Bottom => MouseCursor.SizeNWSE,
            WindowResizeEdges.Right | WindowResizeEdges.Top => MouseCursor.SizeNESW,
            WindowResizeEdges.Left | WindowResizeEdges.Bottom => MouseCursor.SizeNESW,
            _ => null
        };
        if (cursor == null)
            Reset();
        else if (cursor != appliedCursor)
        {
            Mouse.SetCursor(cursor);
            appliedCursor = cursor;
        }
    }

    internal void Reset()
    {
        // Only restore after one of our overrides; leave ordinary cursor frames alone.
        if (appliedCursor == null)
            return;
        Mouse.SetCursor(MouseCursor.Arrow);
        appliedCursor = null;
        // These are MonoGame's shared system cursors, so we don't dispose them.
    }
}

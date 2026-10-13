namespace AgrariaOS.Desktop;

// Per-screen motion, independent of game-time pauses and rail layout.
internal sealed class PelicomAutoHide
{
    internal const double HideDelaySeconds = 1;
    internal const double SlideSeconds = 0.16;
    internal float Shown { get; private set; } = 1;
    private double idleSeconds;
    private double slideSeconds;
    private float slideFrom = 1;
    private float target = 1;

    internal void Reset()
    {
        Shown = slideFrom = target = 1;
        idleSeconds = slideSeconds = 0;
    }

    internal void Update(double elapsedSeconds, bool enabled, bool keepOpen)
    {
        if (!enabled)
        {
            Reset();
            return;
        }
        double elapsed = double.IsFinite(elapsedSeconds) ? Math.Clamp(elapsedSeconds, 0, 0.1) : 0;
        idleSeconds = keepOpen ? 0 : Math.Min(HideDelaySeconds, idleSeconds + elapsed);
        float nextTarget = idleSeconds < HideDelaySeconds ? 1 : 0;
        if (nextTarget != target)
        {
            slideFrom = Shown;
            target = nextTarget;
            slideSeconds = 0;
        }
        if (Shown == target) return;
        slideSeconds += elapsed;
        double duration = SlideSeconds * Math.Abs(target - slideFrom);
        float progress = (float)Math.Clamp(slideSeconds / duration, 0, 1);
        float eased = progress * progress * (3 - 2 * progress);
        Shown = slideFrom + (target - slideFrom) * eased;
    }
}

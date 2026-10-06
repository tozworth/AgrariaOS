using StardewModdingAPI;

namespace AgrariaOS.Configuration;

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public SButton ToggleKey { get; set; } = SButton.F8;
    public int X { get; set; } = 24;
    public int Y { get; set; } = 20;
    public float TextScale { get; set; } = 1f;
    public bool PixelPerfectText { get; set; } = true;
    public float Opacity { get; set; } = 0.95f;
    public bool Use24HourClock { get; set; }
    public bool ShowLocation { get; set; } = true;
    public bool ShowSecurity { get; set; } = true;
    public bool ShowWeather { get; set; } = true;

    internal void Normalize()
    {
        X = Math.Max(0, X);
        Y = Math.Max(0, Y);
        TextScale = float.IsFinite(TextScale) ? Math.Clamp(TextScale, 0.5f, 2f) : 1f;
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.1f, 1f) : 0.95f;
    }

    // Existing 0.85 configs also get the sharper rendering without a migration/reset.
    internal float FontScale => PixelPerfectText ? Math.Clamp(MathF.Round(TextScale), 1f, 2f) : TextScale;
}

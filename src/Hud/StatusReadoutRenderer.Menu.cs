using AgrariaOS.Configuration;
using AgrariaOS.UI;

namespace AgrariaOS.Hud;

partial class StatusReadoutRenderer
{
    internal ContextMenuItem[] GetContextMenuItems(ModConfig config, Action saveConfig)
    {
        ContextMenuItem Toggle(string label, bool isChecked, Action toggle) => new(label, () =>
        {
            toggle();
            saveConfig();
        }, isChecked);

        return new[]
        {
            Toggle("Location", config.ShowLocation, () => config.ShowLocation = !config.ShowLocation),
            Toggle("Security", config.ShowSecurity, () => config.ShowSecurity = !config.ShowSecurity),
            Toggle("Time", config.ShowTime, () => config.ShowTime = !config.ShowTime),
            Toggle("Date", config.ShowDate, () => config.ShowDate = !config.ShowDate),
            Toggle("Day / dusk / night", config.ShowDayPhase, () => config.ShowDayPhase = !config.ShowDayPhase),
            Toggle("Weather", config.ShowWeather, () => config.ShowWeather = !config.ShowWeather),
            Toggle("Money", config.ShowMoney, () => config.ShowMoney = !config.ShowMoney),
            Toggle("Journal", config.ShowJournal, () => config.ShowJournal = !config.ShowJournal),
            Toggle("Zoom controls", config.ShowZoomControls, () => config.ShowZoomControls = !config.ShowZoomControls)
        };
    }
}

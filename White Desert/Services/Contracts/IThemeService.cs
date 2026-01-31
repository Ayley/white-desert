using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace White_Desert.Services.Contracts;

public interface IThemeService
{
    void SetTheme(ThemeVariant themeName);
    void SetBackground(IBrush? brush);
    void SetTransparencyLevel(WindowTransparencyLevel level);
    WindowTransparencyLevel IsMicaEffectActive();
    void Initialize();
}
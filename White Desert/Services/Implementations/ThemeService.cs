using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.Services.Implementations;

public class ThemeService : IThemeService
{
    
    private readonly IFileService<AppSettings> _fileService;
    
    public ThemeService(IFileService<AppSettings> fileService)
    {
        _fileService = fileService;
    }
    
    public void Initialize()
    {
        SetBackground(_fileService.Data!.BrushFromHex());
        SetTransparencyLevel(_fileService.Data!.GetSelectedWindowTheme());
        SetTheme(_fileService.Data!.GetSelectedTheme());
    }

    public void SetTheme(ThemeVariant theme)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = theme;
        }
    }

    public void SetBackground(IBrush? brush)
    {
        var window = GetMainWindow();
        if (window is null) return;
        
        window.Background = brush;
    }


    public WindowTransparencyLevel IsMicaEffectActive()
    {
        bool supportsMica = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                            Environment.OSVersion.Version.Build >= 22000;

        if (!supportsMica) return WindowTransparencyLevel.None;

        return GetMainWindow()?.ActualTransparencyLevel ?? WindowTransparencyLevel.None;
    }

    public void SetTransparencyLevel(WindowTransparencyLevel level)
    {
        var window = GetMainWindow();
        if (window is null) return;

        if (level == WindowTransparencyLevel.None)
        {
            window.Background = _fileService.Data!.BrushFromHex();
            window.TransparencyLevelHint = new WindowTransparencyLevelCollection([WindowTransparencyLevel.None]);
        }
        else
        {
            window.Background = Brushes.Transparent;

            if (level == WindowTransparencyLevel.Mica)
            {
                window.TransparencyLevelHint = new WindowTransparencyLevelCollection(
                [
                    WindowTransparencyLevel.Mica,
                    WindowTransparencyLevel.AcrylicBlur,
                    WindowTransparencyLevel.None
                ]);
            }
            else
            {
                window.TransparencyLevelHint = new WindowTransparencyLevelCollection([level]);
            }
        }
    }

    private Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
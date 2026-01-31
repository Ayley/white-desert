using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace White_Desert.Models;

public class AppSettings
{
    public List<string> GamePaths { get; set; } = [];

    public string SelectedGamePath { get; set; } = "";

    public string? SelectedTheme { get; set; } = "Default";
    
    public string? TransparencyLevel { get; set; } = "Mica";

    public string? Background { get; set; }

    public bool DeleteOldCachedFiles { get; set; } = true;
    public bool ShowHexView { get; set; } = true;
    public bool ShowEditorView { get; set; } = true;
    public bool ShowImageView { get; set; } = true;

    public ThemeVariant GetSelectedTheme()
    {
        return SelectedTheme switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
    
    public WindowTransparencyLevel GetSelectedWindowTheme()
    {
        return TransparencyLevel?.ToLower() switch
        {
            "mica" => WindowTransparencyLevel.Mica,
            "acrylicblur" => WindowTransparencyLevel.AcrylicBlur,
            "blur" => WindowTransparencyLevel.Blur,
            "transparent" => WindowTransparencyLevel.Transparent,
            _ => WindowTransparencyLevel.None
        };
    }
    
    public IBrush BrushFromHex()
    {
        if (Color.TryParse(Background, out var color))
        {
            return new SolidColorBrush(color);
        }

        return Brushes.Transparent;
    }
}
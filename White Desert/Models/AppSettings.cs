using System.Collections.Generic;
using Avalonia.Styling;

namespace White_Desert.Models;

public class AppSettings
{
    public List<string> GamePaths { get; set; } = [];

    public string SelectedGamePath { get; set; } = "";

    public string? SelectedTheme { get; set; } = "Default";

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
}
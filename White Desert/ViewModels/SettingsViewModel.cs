using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using White_Desert.Models;
using White_Desert.Services.Contracts;
using Brushes = Avalonia.Media.Brushes;

namespace White_Desert.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public List<ThemeVariant> AvailableThemes { get; } = [ThemeVariant.Default, ThemeVariant.Dark, ThemeVariant.Light];
    [ObservableProperty] private ThemeVariant? _selectedTheme;

    public List<WindowTransparencyLevel> AvailableWindowThemes { get; } =
    [
        WindowTransparencyLevel.None, WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur,
    ];

    [ObservableProperty] private WindowTransparencyLevel? _selectedWindowTheme;

    public List<BrushItem> AvailableBrushes { get; } = typeof(Brushes)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(IBrush).IsAssignableFrom(p.PropertyType))
        .Select(p => new BrushItem(p.Name, (IBrush)p.GetValue(null)!))
        .OrderBy(b => b.Name)
        .ToList();

    [ObservableProperty] private BrushItem? _selectedBrush;

    [ObservableProperty] private bool _deleteOldCachedFiles;

    [ObservableProperty] private bool _showHexView;
    [ObservableProperty] private bool _showEditorView;
    [ObservableProperty] private bool _showImageView;

    private bool _isSetup = false;

    public string AppVersion => "v1.0.3";

    private readonly IFileService<AppSettings> _fileService;
    private readonly IThemeService _themeService;

    public SettingsViewModel(IFileService<AppSettings> fileService, IThemeService themeService)
    {
        _fileService = fileService;
        _themeService = themeService;

        LoadSettings();
    }

    private void LoadSettings()
    {
        SelectedTheme = _fileService.Data!.GetSelectedTheme();
        SelectedWindowTheme = _fileService.Data!.GetSelectedWindowTheme();
        SelectedBrush = AvailableBrushes.Find(a => a.Name == _fileService.Data!.BrushFromHex().ToString());
        DeleteOldCachedFiles = _fileService.Data!.DeleteOldCachedFiles;
        ShowHexView = _fileService.Data!.ShowHexView;
        ShowEditorView = _fileService.Data!.ShowEditorView;
        ShowImageView = _fileService.Data!.ShowImageView;

        _isSetup = true;
    }

    public void ClearCache()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "White Desert");

        try
        {
            if (Directory.Exists(tempDirectory))
            {
                var files = Directory.GetFiles(tempDirectory);
                foreach (var file in files)
                {
                    File.Delete(file);
                }

                var dirs = Directory.GetDirectories(tempDirectory);
                foreach (var dir in dirs)
                {
                    Directory.Delete(dir, true);
                }

                Log.Information("Directory cleared");
            }
        }
        catch (IOException e)
        {
            Log.Error(e, "File is in use");
        }
        catch (Exception e)
        {
            Log.Error(e, "Unknown error");
        }
    }

    partial void OnSelectedThemeChanged(ThemeVariant? value)
    {
        if (!_isSetup || value == null) return;

        _themeService.SetTheme(value);

        if (_fileService.Data != null)
        {
            _fileService.Data.SelectedTheme = SelectedTheme!.Key.ToString();
        }
    }

    partial void OnSelectedWindowThemeChanged(WindowTransparencyLevel? value)
    {
        if (!_isSetup || value == null) return;


        _themeService.SetTransparencyLevel(value!.Value);

        if (_fileService.Data != null)
        {
            _fileService.Data.TransparencyLevel = value.Value.ToString();
        }
    }

    partial void OnSelectedBrushChanged(BrushItem? value)
    {
        if (!_isSetup) return;
        
        _fileService.Data!.Background = GetHexFromBrush(value.Brush);
        
        _themeService.SetBackground(value.Brush);
    }
    
    partial void OnShowHexViewChanged(bool value)
    {
        _fileService.Data!.ShowHexView = value;
    }

    partial void OnShowEditorViewChanged(bool value)
    {
        _fileService.Data!.ShowEditorView = value;
    }

    partial void OnShowImageViewChanged(bool value)
    {
        _fileService.Data!.ShowImageView = value;
    }

    partial void OnDeleteOldCachedFilesChanged(bool value)
    {
        _fileService.Data!.DeleteOldCachedFiles = value;
    }
    
    public string GetHexFromBrush(IBrush? brush)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            var c = solidBrush.Color;
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    
        return "#00000000"; 
    }
}
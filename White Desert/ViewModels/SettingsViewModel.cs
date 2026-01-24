using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public List<ThemeVariant> AvailableThemes { get; } = [ThemeVariant.Default, ThemeVariant.Dark, ThemeVariant.Light];
    [ObservableProperty] private ThemeVariant? _selectedTheme;

    [ObservableProperty] private bool _deleteOldCachedFiles;

    [ObservableProperty] private bool _showHexView;
    [ObservableProperty] private bool _showEditorView;
    [ObservableProperty] private bool _showImageView;

    private bool _isSetup = false;

    public string AppVersion => "v1.0.0";

    private readonly IFileService<AppSettings> _fileService;

    public SettingsViewModel(IFileService<AppSettings> fileService)
    {
        _fileService = fileService;

        LoadSettings();
    }

    private void LoadSettings()
    {
        SelectedTheme = _fileService.Data!.GetSelectedTheme();
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

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = value;
        }

        if (_fileService.Data != null)
        {
            _fileService.Data.SelectedTheme = SelectedTheme!.Key.ToString();
        }
    }

    partial void OnShowHexViewChanged(bool value)
    {
        _fileService.Data!.ShowHexView = value;
Console.WriteLine(value);
    }

    partial void OnShowEditorViewChanged(bool value)
    {
        _fileService.Data!.ShowEditorView = value;

    }

    partial void OnShowImageViewChanged(bool value)
    {
        _fileService.Data!.ShowImageView = value;

    }
}
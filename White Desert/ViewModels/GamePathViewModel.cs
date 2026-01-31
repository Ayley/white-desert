using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.ViewModels;

public partial class GamePathViewModel : ViewModelBase, IProgress<string>
{
    [ObservableProperty] private string _searchStatus = "Search";
    [ObservableProperty] private bool _canSearch = true;
    [ObservableProperty] private string _openStatus = "Open";
    [ObservableProperty] private bool _canOpen;
    [ObservableProperty] private bool _canRemove;
    [ObservableProperty] private bool _canCancelSearch;
    [ObservableProperty] private bool _hasSavedPaths;
    [ObservableProperty] private string _selectedGamePath = "";
    [ObservableProperty] private string _textboxGamePath = "";
    [ObservableProperty] private bool _canTextboxGamePathAdd;

    public ObservableCollection<string> GamePaths { get; } = [];
    public ObservableCollection<SelectableDrive> Drives { get; } = [];

    private readonly IGameSearchService _gameSearchService;
    private readonly IFileService<AppSettings> _appSettingsService;
    private readonly INavigationService _navigationService;

    private CancellationTokenSource? _searchTokenSource;

    public GamePathViewModel(IGameSearchService gameSearchService, IFileService<AppSettings> appSettingsService,
        INavigationService navigationService)
    {
        _gameSearchService = gameSearchService;
        _appSettingsService = appSettingsService;
        _navigationService = navigationService;

        InitializeViewModel();
    }

    private async void InitializeViewModel()
    {
        await _appSettingsService.LoadAsync();

        GamePaths.CollectionChanged += (_, _) => { HasSavedPaths = GamePaths.Count > 0; };

        PopulateDrives();
        SyncSettingsToCollection();

        SelectedGamePath = _appSettingsService.Data!.SelectedGamePath;
    }

    private void PopulateDrives()
    {
        foreach (var drive in _gameSearchService.GetDrives())
        {
            var selectableDrive = new SelectableDrive
            {
                Drive = drive,
                IsSelected = drive.Equals(@"C:\")
            };

            selectableDrive.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectableDrive.IsSelected))
                {
                    UpdateSearchAbility();
                }
            };

            Drives.Add(selectableDrive);
        }
    }

    private void SyncSettingsToCollection()
    {
        foreach (var path in _appSettingsService.Data!.GamePaths)
        {
            GamePaths.Add(path);
        }
    }

    private void UpdateSearchAbility()
    {
        CanSearch = Drives.Any(d => d.IsSelected);
    }

    public async Task SearchAsync()
    {
        CanSearch = false;
        CanCancelSearch = true;

        _searchTokenSource = new CancellationTokenSource();
        var token = _searchTokenSource.Token;

        var selectedDrives = Drives.Where(d => d.IsSelected).Select(d => d.Drive).ToList();

        if (selectedDrives.Count > 0)
        {
            try
            {
                await _gameSearchService.SearchGamePathsInDrivesAsync(selectedDrives, this,
                    token, AddUniquePath);
            }
            catch (Exception e)
            {
                /* Ignore */
            }

            if (_searchTokenSource.IsCancellationRequested)
            {
                SearchStatus = "Canceled";
                await Task.Delay(2000);
            }
        }
        
        SearchStatus = "Search";
        CanSearch = true;
        CanCancelSearch = false;
        
        _searchTokenSource.Dispose();
        _searchTokenSource = null;
    }

    public async Task BrowseFolderAsync(Visual visual)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        if (topLevel == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Select Black Desert installation directory",
            AllowMultiple = false,
            FileTypeFilter =
                [new FilePickerFileType("Black Desert Launcher") { Patterns = ["BlackDesertLauncher.exe"] }]
        };

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (result.Count == 0) return;

        var cleanPath = result[0].Path.LocalPath.Replace("\\BlackDesertLauncher.exe", "")
            .Replace("/BlackDesertLauncher.exe", "");

        if (_gameSearchService.IsGameDirectory(cleanPath))
        {
            TextboxGamePath = cleanPath;
        }
        else if (await ShowInvalidPathDialog(false))
        {
            await BrowseFolderAsync(visual);
        }
    }

    public void CancelSearch()
    {
        _searchTokenSource?.Cancel();
    }

    public async Task AddManualPathAsync()
    {
        if (string.IsNullOrWhiteSpace(TextboxGamePath))
        {
            await ShowInvalidPathDialog(true);
            return;
        }

        AddUniquePath(TextboxGamePath);
        TextboxGamePath = string.Empty;
    }

    private void AddUniquePath(string path)
    {
        if (!GamePaths.Contains(path))
        {
            GamePaths.Add(path);

            _appSettingsService.Data!.GamePaths = GamePaths.ToList();

            _appSettingsService.SaveAsync();
        }
    }

    public void RemoveSelectedPath()
    {
        if (string.IsNullOrEmpty(SelectedGamePath)) return;

        GamePaths.Remove(SelectedGamePath);
        SelectedGamePath = string.Empty;
    }

    public async Task NavigateToGame()
    {
        _navigationService.NavigateTo(typeof(GameViewModel));

        if (_navigationService.CurrentViewModel is GameViewModel gameViewModel)
        {
            await gameViewModel.InitializeAsync();
        }
    }

    private async Task<bool> ShowInvalidPathDialog(bool isInfoOnly)
    {
        var dialog = new ContentDialog
        {
            Title = "Invalid Game Path",
            Content =
                "The file 'BlackDesertLauncher.exe' was not found in the selected folder. Please select the official Black Desert Online installation directory.",
            PrimaryButtonText = isInfoOnly ? "" : "Try Again",
            CloseButtonText = isInfoOnly ? "OK" : "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public void Report(string value) => SearchStatus = value;

    partial void OnSelectedGamePathChanged(string value)
    {
        CanOpen = !string.IsNullOrEmpty(value);
        CanRemove = CanOpen;
        _appSettingsService.Data!.SelectedGamePath = value;
    }

    partial void OnTextboxGamePathChanged(string value)
    {
        CanTextboxGamePathAdd = _gameSearchService.IsGameDirectory(value);
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using White_Desert.Models;
using White_Desert.Services.Contracts;
using White_Desert.Helper;
using White_Desert.Helper.Files;
using White_Desert.Helper.Image;
using White_Desert.Helper.Interop;
using White_Desert.Messages;
using White_Desert.Models.GhostBridge;

namespace White_Desert.ViewModels;

public partial class GameViewModel : ViewModelBase
{
    private readonly AvaloniaList<BdoNode> _folderNodes = [];
    private readonly AvaloniaList<BdoNode> _searchNodes = [];
    private readonly HierarchicalTreeDataGridSource<BdoNode> _folderSource;
    private readonly HierarchicalTreeDataGridSource<BdoNode> _searchSource;

    [ObservableProperty] private HierarchicalTreeDataGridSource<BdoNode> _activeSource;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private bool _showHexView;
    [ObservableProperty] private bool _showEditorView;
    [ObservableProperty] private bool _showImageView;
    
    [ObservableProperty] private int _selectedTabStripIndex;
    [ObservableProperty] private int _selectedPageContainerIndex;

    [ObservableProperty] private bool _canExtract;
    [ObservableProperty] private string _extractText = "Extract";
    private CancellationTokenSource? _tokenSource;

    private readonly BdoNode _placeholder = new("", "", false);
    private BdoNode? _selectedNode;
    
    private readonly IFileService<AppSettings> _fileService;
    private readonly IPazService _pazService;
    private readonly ICursorService _cursorService;

    public GameViewModel(IFileService<AppSettings> fileService, IPazService pazService, ICursorService cursorService)
    {
        _fileService = fileService;
        _pazService = pazService;
        _cursorService = cursorService;

        _folderSource = CreateSource(_folderNodes, "Name");
        _folderSource.RowExpanded += OnRowExpanded;

        _searchSource = CreateSource(_searchNodes, "Folder / File");

        _folderSource.RowSelection!.SelectionChanged += RowSelectionOnSelectionChanged;
        _searchSource.RowSelection!.SelectionChanged += RowSelectionOnSelectionChanged;

        ActiveSource = _folderSource;
    }

    private HierarchicalTreeDataGridSource<BdoNode> CreateSource(AvaloniaList<BdoNode> nodes, string title)
    {
        var source = new HierarchicalTreeDataGridSource<BdoNode>(nodes)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<BdoNode>(
                    new TextColumn<BdoNode, string>(title, x => x.Name, GridLength.Star), x => x.Children)
            }
        };
        source.RowSelection!.SingleSelect = false;
        return source;
    }

    public async Task InitializeAsync()
    {
        _cursorService.SetWaitCursor();

        await Task.Run(() =>
        {
            try
            {
                _pazService.Initialize(_fileService.Data!.SelectedGamePath);
                var folders = _pazService.GetAllFolders();

                Dispatcher.UIThread.InvokeAsync(() => LoadFolders(folders));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Can't initialize game data");
            }
            finally
            {
                _cursorService.SetDefaultCursor();
            }
        });
    }

    private async void RowSelectionOnSelectionChanged(object? sender,
        TreeSelectionModelSelectionChangedEventArgs<BdoNode> e)
    {
        var node = ActiveSource.RowSelection!.SelectedItem;
        _selectedNode = node;
        if (node?.EntryIndex == null) return;

        _tokenSource?.Cancel();
        _tokenSource = new CancellationTokenSource();
        var token = _tokenSource.Token;

        CanExtract = true;
        WeakReferenceMessenger.Default.Send(new InitSelectedFileChanged());

        try
        {
            var result = await Task.Run(async () => await LoadFileInBackgroundAsync(node, token), token);

            if (token.IsCancellationRequested || result == null) return;

            UpdateViewsVisibility(node.Name, node.IsFolder);
            WeakReferenceMessenger.Default.Send(new SelectedFileChanged(result.RawData, node, result.Content,
                result.TempFile));
        }
        catch (OperationCanceledException)
        {
            /* Ignore */
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error selecting file {FileName}", node.Name);
        }
    }

    private record FileLoadResult(PazFile RawData, byte[] Content, string TempFile);

    private async Task<FileLoadResult?> LoadFileInBackgroundAsync(BdoNode node, CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested) return null;

            var rawData = _pazService[node.EntryIndex!.Value];
            if (token.IsCancellationRequested) return null;

            byte[]? content = null;
            var tempFile = string.Empty;

            if (TempFileHelper.TryGetProcessedFile(rawData, "temp", out var path))
            {
                if (token.IsCancellationRequested) return null;
                
                content = await File.ReadAllBytesAsync(path, token);
                tempFile = path;
            }
            else
            {
                content = _pazService.GetFileBytes(rawData);

                if (content == null || token.IsCancellationRequested) return null;

                tempFile = TempFileHelper.SaveProcessedFile(rawData, content, "raw",
                    _fileService.Data!.DeleteOldCachedFiles);
            }

            return new FileLoadResult(rawData, content, tempFile);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void UpdateViewsVisibility(string file, bool isFolder)
    {
        ShowHexView = _fileService.Data!.ShowHexView && !isFolder;

        if (SelectedPageContainerIndex == 1 && !ShowHexView)
        {
            SelectedPageContainerIndex = 0;
            SelectedTabStripIndex = 0;
        }
        
        var isImage = ImageHelper.GetImageType(file) != ImageType.None;

        ShowEditorView = _fileService.Data!.ShowEditorView && !isImage && !isFolder;
        
        if (SelectedPageContainerIndex == 2 && !ShowEditorView)
        {
            SelectedPageContainerIndex = 0;
            SelectedTabStripIndex = 0;
        }
        
        ShowImageView = _fileService.Data!.ShowImageView && isImage && !isFolder;
        
        if (SelectedPageContainerIndex == 3 && !ShowImageView)
        {
            SelectedPageContainerIndex = 0;
            SelectedTabStripIndex = 0;
        }
    }

    partial void OnSelectedTabStripIndexChanged(int value)
    {
        SelectedPageContainerIndex = value;
    }

    public async Task SearchAsync()
    {
        using (_cursorService.BusyScope())
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                ActiveSource = _folderSource;
                return;
            }

            IsSearching = true;

            var searchTree = await Task.Run(() =>
                TreeDataGridHelper.BuildSearchTree(_pazService, SearchText));

            _searchNodes.Clear();
            _searchNodes.AddRange(searchTree);

            ActiveSource = _searchSource;

            await Task.Delay(50);

            await Dispatcher.UIThread.InvokeAsync(() => ExpandAllSearchResults(_searchSource.Items),
                DispatcherPriority.Background);

            IsSearching = false;
        }
    }

    public async Task Extract(ExtractType type)
    {
        _cursorService.SetWaitCursor();
        var selectedItems = ActiveSource.RowSelection?.SelectedItems.Cast<BdoNode>().ToList();
        if (selectedItems == null || selectedItems.Count == 0) return;

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner == null) return;

        var topLevel = TopLevel.GetTopLevel(owner);
        var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            { Title = "Select Export Destination" });
        if (folders.Count == 0) return;

        var outputRoot = folders[0].Path.LocalPath;
        CanExtract = false;

        try
        {
            ExtractText = "Scanning selection...";
            
            var isSearchMode = ActiveSource == _searchSource;
            
            var indices = await Task.Run(() =>
                TreeDataGridHelper.CollectIndicesParallel(_pazService, selectedItems, isSearchMode));
            
            if (indices.Count > 0)
            {
                await Task.Run(() => _pazService.ExtractFilesBatch(outputRoot, indices, type,
                    (current, total) => ExtractText = $"Extracting: {current} / {total}"));

                ExtractText = $"Successfully extracted {indices.Count} files!";
                _cursorService.SetDefaultCursor();
                await Task.Delay(2000);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting files");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _cursorService.SetDefaultCursor();
            ExtractText = "Extract";
            CanExtract = true;
        }
    }

    public void CopyPath(string type)
    {
        if (_selectedNode == null) return;

        var result = type switch
        {
            "Name" => _selectedNode.Name,
            "Full" => _selectedNode.FullPath,
            "Folder" => Path.GetDirectoryName(_selectedNode.FullPath) ?? string.Empty,
            _ => string.Empty
        };

        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow.Clipboard
            .SetTextAsync(result);
    }

    private async void LoadFolders(List<FolderNameTuple> allFolder)
    {
        _cursorService.SetWaitCursor();

        var rootNodes = await Task.Run(() =>
            TreeDataGridHelper.BuildFolderTree(allFolder, _placeholder));

        _folderNodes.Clear();
        _folderNodes.AddRange(rootNodes);

        _cursorService.SetDefaultCursor();
    }

    private async void OnRowExpanded(object? sender, RowEventArgs<HierarchicalRow<BdoNode>> e)
    {
        if (e.Row.Model is { IsFolder: true, WasLoaded: false } node)
        {
            await LoadContentForFolderAsync(node);
        }
    }

    private async Task LoadContentForFolderAsync(BdoNode parent)
    {
        if (parent.WasLoaded) return;

        using (_cursorService.BusyScope())
        {
            var newChildren = await Task.Run(() =>
            {
                var files = TreeDataGridHelper.GetFolderChildren(_pazService, parent);

                return files;
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (parent.Children == null) return;

                var placeholder = parent.Children.FirstOrDefault(c => c == _placeholder);
                if (placeholder != null)
                {
                    parent.Children.Remove(placeholder);
                }

                parent.Children.AddRange(newChildren);
            });

            parent.WasLoaded = true;
        }
    }

    private void ExpandAllSearchResults(IEnumerable<BdoNode> nodes, IndexPath parentPath = default)
    {
        if (_searchNodes.Count > 150) return;
        var index = 0;
        foreach (var node in nodes)
        {
            var currentPath = parentPath.Append(index++);
            if (!node.IsFolder || node.Children == null) continue;
            _searchSource.Expand(currentPath);
            if (node.Children.Count < 50) ExpandAllSearchResults(node.Children, currentPath);
        }
    }
}
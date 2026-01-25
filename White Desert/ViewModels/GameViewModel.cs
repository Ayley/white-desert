using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
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

    [ObservableProperty] private bool _canExtract;
    [ObservableProperty] private string _extractText = "Extract";

    private readonly BdoNode _placeholder = new("", "", false);

    private ILookup<string, FolderNameTuple> _folderLookup = null!;
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

    private void RowSelectionOnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<BdoNode> e)
    {
        var node = ActiveSource.RowSelection!.SelectedItem;
        if (node == null) return;

        CanExtract = true;

        if (node.EntryIndex == null) return;

        PazFile? rawData = null;

        try
        {
            rawData = _pazService[node.EntryIndex.Value];
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }

        if (rawData == null) return;

        byte[]? content;
        string tempFile;

        if (TempFileHelper.ExistsTempFile(rawData.Value, out var path))
        {
            content = File.ReadAllBytes(path);
            tempFile = path;
        }
        else
        {
            content = _pazService.GetFileBytes(rawData.Value);
            tempFile = TempFileHelper.SaveTempFile(rawData.Value, content!, _fileService.Data!.DeleteOldCachedFiles);
        }

        UpdateViewsVisibility(node.Name);

        WeakReferenceMessenger.Default.Send(new SelectedFileChanged(rawData, node, content, tempFile));
    }

    private void UpdateViewsVisibility(string file)
    {
        ShowHexView = _fileService.Data!.ShowHexView;

        var isImage = ImageHelper.GetImageType(file) != ImageType.None;

        ShowEditorView = _fileService.Data!.ShowEditorView && !isImage;
        ShowImageView = _fileService.Data!.ShowImageView && isImage;
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

    public async Task Extract(Visual visual)
    {
        using (_cursorService.BusyScope())
        {
            var selectedItems = ActiveSource.RowSelection?.SelectedItems.Cast<BdoNode>().ToList();
            if (selectedItems == null || selectedItems.Count == 0) return;

            var topLevel = TopLevel.GetTopLevel(visual);
            var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                { Title = "Select Export Destination" });
            if (folders.Count == 0) return;

            var outputRoot = folders[0].Path.LocalPath;
            CanExtract = false;

            try
            {
                ExtractText = "Scanning selection...";

                var indices = await Task.Run(() =>
                    TreeDataGridHelper.CollectIndicesParallel(_pazService, selectedItems, _folderLookup));

                if (indices.Count > 0)
                {
                    await Task.Run(() => _pazService.ExtractFilesBatch(outputRoot, indices,
                        (current, total) => ExtractText = $"Extracting: {current} / {total}"));

                    ExtractText = $"Successfully extracted {indices.Count} files!";
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting files");
            }
            finally
            {
                ExtractText = "Extract";
                CanExtract = true;
            }
        }
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
                if(parent.Children == null) return;
                
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
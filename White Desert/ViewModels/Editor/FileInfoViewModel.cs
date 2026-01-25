using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using White_Desert.Helper.Files;
using White_Desert.Messages;
using White_Desert.Models;

namespace White_Desert.ViewModels.Editor;

public partial class FileInfoViewModel : ViewModelBase
{
    
    [ObservableProperty] private PazFileInfo? _selectedPazFile;

    public FileInfoViewModel()
    {
        RegisterWeakReferences();
    }

    private void RegisterWeakReferences()
    {
        WeakReferenceMessenger.Default.Register<SelectedFileChanged>(this, SelectedFileChanged);
        WeakReferenceMessenger.Default.Register<InitSelectedFileChanged>(this, InitSelectedFileChanged);
    }

    private void InitSelectedFileChanged(object recipient, InitSelectedFileChanged message)
    {
        SelectedPazFile = null;
    }

    private void SelectedFileChanged(object recipient, SelectedFileChanged message)
    {
        if(!message.File.HasValue) return;
        
        SelectedPazFile = new PazFileInfo
        {
            RawData = message.File.Value,
            FileName = message.Node.Name,
            FolderName = message.Node.FullPath,
            cachedFilePaths = new ObservableCollection<string>(TempFileHelper.GetExistentTempFiles(message.File.Value))
        };
    }
    
    [RelayCommand]
    private void DeleteCachedFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            SelectedPazFile?.cachedFilePaths.Remove(filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting file");
        }
    }
}
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using White_Desert.Helper;
using White_Desert.Helper.Files;
using White_Desert.Messages;
using White_Desert.Models;
using White_Desert.Services.Contracts;

namespace White_Desert.ViewModels.Editor;

public partial class TextEditorViewModel : ViewModelBase
{
    [ObservableProperty] private TextDocument _editorDocument = new();
    [ObservableProperty] private IHighlightingDefinition? _highlightingDefinition;

    private readonly IFileService<AppSettings> _fileService;
    private readonly IPazService _pazService;

    private CancellationTokenSource? _tokenSource;
    
    public TextEditorViewModel(IFileService<AppSettings> fileService, IPazService pazService)
    {
        _fileService = fileService;
        _pazService = pazService;

        RegisterWeakReferences();
        LoadEditorFormatting();
    }

    private void LoadEditorFormatting()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        HighlightingMangerHelper.Register("Json", "Json.xshd", [".json"]);
        HighlightingMangerHelper.Register("JavaScript", "JavaScript.xshd", [".js"]);
        HighlightingMangerHelper.Register("CSS", "Css.xshd", [".css"]);
        HighlightingMangerHelper.Register("Lua", "Lua.xshd", [".lua", ".luac"]);
        HighlightingMangerHelper.Register("XML", "Xml.xshd", [".xml", ".ui", ".layout"]);
        HighlightingMangerHelper.Register("HTML", "Html.xshd", [".html", ".htm"]);
    }

    private void RegisterWeakReferences()
    {
        WeakReferenceMessenger.Default.Register<SelectedFileChanged>(this, SelectedFileChanged);
    }
    
    private async void SelectedFileChanged(object recipient, SelectedFileChanged message)
    {
        _tokenSource?.Cancel();
        _tokenSource = new CancellationTokenSource();
        var token = _tokenSource.Token;

        try
        {
            EditorDocument.Text = string.Empty;

            if (message.Node.IsFolder) return;
            if (!_fileService.Data!.ShowEditorView) return;
            if (message.Content == null || message.Content.Length == 0) return;
            if (!message.File.HasValue) return;
            
            var extension = Path.GetExtension(message.Node.Name).ToLower();
            var typeTag = extension.Replace(".", ""); 
            byte[]? content = null;
            
            if (TempFileHelper.TryGetProcessedFile(message.File.Value, typeTag, out var path))
            {
                content = await File.ReadAllBytesAsync(path, token);
            }
            else
            {
                if (extension == ".luac")
                {
                    content = await Task.Run(() => _pazService.DecompileLua(message.File.Value), token);
                }
                else
                {
                    content = message.Content;
                }

                if (content is not  null && content.Length > 0)
                {
                    _ = Task.Run(() => TempFileHelper.SaveProcessedFile(message.File.Value, content, typeTag, _fileService.Data!.DeleteOldCachedFiles), token);
                }
            }
            
            if (token.IsCancellationRequested || content == null) return;

            var text = await Task.Run(() => 
            {
                return extension switch
                {
                    ".txt" => Encoding.GetEncoding(949).GetString(content),
                    ".luac" => Encoding.Default.GetString(content),
                    _ => Encoding.UTF8.GetString(content)
                };
            }, token);

            if (token.IsCancellationRequested) return;
            EditorDocument.Text = text;
            EditorDocument.FileName = message.Node.Name;
            HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension(extension);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load text file {FileName}", message.Node.Name);
        }
    }
}
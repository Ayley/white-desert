using System;
using System.IO;
using System.Text;
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

    private void SelectedFileChanged(object recipient, SelectedFileChanged message)
    {
        EditorDocument.Text = string.Empty;

        if (message.Node.IsFolder) return;
        if (!_fileService.Data!.ShowEditorView) return;
        if (message.Content == null || message.Content.Length == 0) return;

        var extension = Path.GetExtension(message.Node.Name).ToLower();


        EditorDocument.FileName = message.Node.Name;
        HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension(extension);

        EditorDocument.Text = extension switch
        {
            ".txt" => Encoding.GetEncoding(949).GetString(message.Content),
            ".luac" => Encoding.Default.GetString(
                _pazService.DecompileLua(_pazService[message.Node.EntryIndex!.Value])),
            _ => Encoding.UTF8.GetString(message.Content)
        };
    }
}
using System;
using System.Xml;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace White_Desert.Helper;

public static class HighlightingMangerHelper
{
    
    public static void Register(string name, string resourcePath, string[] extensions)
    {
        var uri = new Uri($"avares://White Desert/Assets/{resourcePath}");
        if (!AssetLoader.Exists(uri)) return;
        
        try
        {
            using var stream = AssetLoader.Open(uri);
            using var reader = XmlReader.Create(stream);
            var def = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting(name, extensions, def);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
}
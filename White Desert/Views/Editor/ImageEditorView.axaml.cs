using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using White_Desert.ViewModels.Editor;

namespace White_Desert.Views.Editor;

public partial class ImageEditorView : UserControl
{
    public ImageEditorView()
    {
        InitializeComponent();
        
        DataContext = ServiceInjection.Services.GetRequiredService<ImageEditorViewModel>();

    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using White_Desert.ViewModels.Editor;

namespace White_Desert.Views.Editor;

public partial class TextEditorView : UserControl
{
    public TextEditorView()
    {
        InitializeComponent();

        DataContext = ServiceInjection.Services.GetRequiredService<TextEditorViewModel>();
    }
}
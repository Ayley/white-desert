using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using White_Desert.ViewModels.Editor;

namespace White_Desert.Views.Editor;

public partial class FileInfoView : UserControl
{
    
    public FileInfoView()
    {
        InitializeComponent();
        
        DataContext = ServiceInjection.Services.GetRequiredService<FileInfoViewModel>();
    }
}
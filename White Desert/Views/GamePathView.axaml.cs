using Avalonia.Controls;
using White_Desert.ViewModels;

namespace White_Desert.Views;

public partial class GamePathView : UserControl
{
    public GamePathView()
    {
        InitializeComponent();
    }
    
    public GamePathView(GamePathViewModel gamePathViewModel)
    {
        DataContext = gamePathViewModel;
        InitializeComponent();
    }
}
using CommunityToolkit.Mvvm.ComponentModel;

namespace White_Desert.Models;

public partial class SelectableDrive : ObservableObject
{

    [ObservableProperty] private string _drive = "";
    [ObservableProperty] private bool _isSelected = false;
    
}
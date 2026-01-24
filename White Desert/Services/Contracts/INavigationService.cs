using System;
using Avalonia.Controls;
using White_Desert.ViewModels;

namespace White_Desert.Services.Contracts;

public interface INavigationService
{
    public ViewModelBase CurrentViewModel { get; set; }
    
    public event Action<Type>? Navigated;

    public void SetContentControl(ContentControl contentControl);
    
    public bool NavigateTo(Type pageType);

}
using System;
using Avalonia.Controls;
using White_Desert.Services.Contracts;
using White_Desert.ViewModels;

namespace White_Desert.Services.Implementations;

public class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    
    private ContentControl? _contentControl;
    private ViewModelBase? _viewModel;
    
    public ViewModelBase CurrentViewModel { get; set; }
    public event Action<Type>? Navigated;

    public void SetContentControl(ContentControl contentControl)
    {
        _contentControl = contentControl;
    }

    public bool NavigateTo(Type pageType)
    {
        if(_contentControl == null) return false;
        if (!typeof(ViewModelBase).IsAssignableFrom(pageType)) return false;
        
        var vm = serviceProvider.GetService(pageType);

        CurrentViewModel = vm as ViewModelBase;
        
        if(vm == null) return  false;
        _viewModel = vm as ViewModelBase;
        _contentControl.Content = _viewModel;
        
        Navigated?.Invoke(pageType);
        
        return true;
    }
}
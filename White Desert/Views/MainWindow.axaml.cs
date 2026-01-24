using System;
using System.Linq;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using White_Desert.Services.Contracts;
using White_Desert.ViewModels;

namespace White_Desert.Views;

public partial class MainWindow : Window
{
    
    private readonly INavigationService _navigationService;
    
    public MainWindow(INavigationService navigationService, MainWindowViewModel mainWindowViewModel)
    {
        _navigationService = navigationService;
        InitializeComponent();
        
        DataContext = mainWindowViewModel;
        
        navigationService.SetContentControl(NavView);
        navigationService.NavigateTo(typeof(GamePathViewModel));
        
        navigationService.Navigated += UpdateSelectedItem;
    }

    private void NavView_OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.IsSettingsInvoked)
        {
            _navigationService.NavigateTo(typeof(SettingsViewModel));
            return;
        }
        
        switch (e.InvokedItemContainer.Tag)
        {
            case "unpack":
                _navigationService.NavigateTo(typeof(GameViewModel));
                break;
            case "game_paths":
                _navigationService.NavigateTo(typeof(GamePathViewModel));
                break;
        }
    }
    
    private void UpdateSelectedItem(Type viewModelType)
    {
        var item = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(x => x.Tag?.ToString() == GetTagForViewModel(viewModelType));

        if (item != null)
        {
            NavView.SelectedItem = item;
        }
    }

    private string GetTagForViewModel(Type type)
    {
        if (type == typeof(GamePathViewModel)) return "game_paths";
        if (type == typeof(GameViewModel)) return "unpack";
        return string.Empty;
    }
}
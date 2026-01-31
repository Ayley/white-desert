using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using White_Desert.Models;
using White_Desert.Services.Contracts;
using White_Desert.Views;

namespace White_Desert;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var si = new ServiceInjection();
        si.Initialize();
        var services = si.Build();

        LoadInitialTheme(services);
        
        var settings = services.GetRequiredService<IFileService<AppSettings>>();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            desktop.MainWindow = services.GetRequiredService<MainWindow>();

            services.GetRequiredService<IThemeService>().SetTransparencyLevel(settings.Data!.GetSelectedWindowTheme());
            if (settings.Data!.GetSelectedWindowTheme() == WindowTransparencyLevel.None)
            {
                desktop.MainWindow.Background = settings.Data!.BrushFromHex();
            }

            desktop.ShutdownRequested += async (_, _) =>
            {
                await settings.SaveAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    
    private void LoadInitialTheme(IServiceProvider services)
    {
        var settings = services.GetRequiredService<IFileService<AppSettings>>().Load();
        this.RequestedThemeVariant = settings.GetSelectedTheme();
    }
}
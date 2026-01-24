using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using White_Desert.Models;
using White_Desert.Services.Contracts;
using White_Desert.Services.Implementations;
using White_Desert.ViewModels;
using White_Desert.ViewModels.Editor;
using White_Desert.Views;
using White_Desert.Views.Editor;

namespace White_Desert;

public class ServiceInjection
{

    public static ServiceProvider Services {get; private set;} = null!;

    private readonly IServiceCollection _collection = new ServiceCollection();
    
    public void Initialize()
    {
        //Services
        _collection.AddSingleton<IGameSearchService, GameSearchService>();
        _collection.AddSingleton<INavigationService, NavigationService>();
        _collection.AddSingleton<IFileService<AppSettings>, FileService<AppSettings>>();
        _collection.AddSingleton<IPazService, PazService>();
        _collection.AddSingleton<ICursorService, CursorService>();
        
        //App view models
        _collection.AddTransient<MainWindowViewModel>();
        _collection.AddTransient<GamePathViewModel>();
        _collection.AddTransient<SettingsViewModel>();
        _collection.AddSingleton<GameViewModel>();
        
        //Editor view models
        _collection.AddTransient<FileInfoViewModel>();
        _collection.AddTransient<HexEditorViewModel>();
        _collection.AddTransient<ImageEditorViewModel>();
        _collection.AddTransient<TextEditorViewModel>();

        //App views
        _collection.AddTransient<MainWindow>();
        _collection.AddTransient<GamePathView>();
        _collection.AddTransient<GameView>();
        _collection.AddTransient<SettingsView>();
        
        //Editor views
        _collection.AddTransient<FileInfoView>();
        _collection.AddTransient<HexEditorView>();
        _collection.AddTransient<ImageEditorView>();
        _collection.AddTransient<TextEditorView>();
    }

    public ServiceProvider Build()
    {
        Services = _collection.BuildServiceProvider();
        return Services;
    }

    public static bool IsServiceInitialized<T>() => Services.GetServices<T>().Any();
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WaveDL.Data;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services;
using WaveDL.Services.Abstractions;
using WaveDL.Services.Providers;
using WaveDL.Services.YtDlp;
using WaveDL.ViewModels;

namespace WaveDL;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        UnhandledException += OnUnhandledException;
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    public Window? MainWindow { get; private set; }

    public static T GetService<T>() where T : class => Current.Services.GetRequiredService<T>();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logger = Services.GetRequiredService<ILogger<App>>();

        var settings = Services.GetRequiredService<ISettingsService>();
        settings.Load();

        try
        {
            using var db = Services.GetRequiredService<IDbContextFactory<WaveDlDbContext>>().CreateDbContext();
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Initialisation de la base d'historique impossible.");
        }

        Services.GetRequiredService<INotificationService>().TryRegister();

        MainWindow = new MainWindow();
        UiInterop.WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
        MainWindow.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WaveDL",
            "logs");

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(logDirectory, LogLevel.Information));
        });

        services.AddHttpClient("default", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IToolchainService, ToolchainService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<YtDlpEngine>();

        services.AddSingleton<IExternalMetadataProvider, SpotifyMetadataProvider>();
        services.AddSingleton<IExternalMetadataProvider, DeezerMetadataProvider>();
        services.AddSingleton<IExternalMetadataProvider, AppleMusicMetadataProvider>();

        services.AddDbContextFactory<WaveDlDbContext>((provider, options) =>
        {
            var settings = provider.GetRequiredService<ISettingsService>();
            options.UseSqlite($"Data Source={settings.DatabasePath}");
        });

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DownloadsViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<TrackDetailViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<LinkImportViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.GetRequiredService<ILogger<App>>().LogError(e.Exception, "Exception non gérée : {Message}", e.Message);
        e.Handled = true;
    }
}

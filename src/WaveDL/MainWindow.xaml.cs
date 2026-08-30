using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services.Abstractions;
using WaveDL.ViewModels;
using WaveDL.Views;

namespace WaveDL;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly IToolchainService _toolchain;
    private readonly IUpdateService _updates;
    private readonly IClipboardService _clipboard;
    private readonly ShellViewModel _shell;

    public MainWindow()
    {
        InitializeComponent();

        _navigation = App.GetService<INavigationService>();
        _settings = App.GetService<ISettingsService>();
        _toolchain = App.GetService<IToolchainService>();
        _updates = App.GetService<IUpdateService>();
        _clipboard = App.GetService<IClipboardService>();
        _shell = App.GetService<ShellViewModel>();

        // Instantiate the downloads view-model up front so it observes every job from the start.
        _ = App.GetService<DownloadsViewModel>();

        Title = "WaveDL";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TrySetMicaBackdrop();
        ResizeToDefault();

        RootGrid.DataContext = _shell;

        RegisterRoutes();
        _navigation.Initialize(ContentFrame);
        _navigation.Navigated += OnNavigated;

        UiInterop.ThemeApplier = ApplyTheme;
        ApplyTheme(_settings.Current.Theme);

        _clipboard.Start();
        _navigation.Navigate("home");

        RunStartupChecksAsync();
    }

    private void RegisterRoutes()
    {
        _navigation.Register("home", typeof(HomePage));
        _navigation.Register("search", typeof(SearchPage));
        _navigation.Register("link", typeof(LinkImportPage));
        _navigation.Register("downloads", typeof(DownloadsPage));
        _navigation.Register("history", typeof(HistoryPage));
        _navigation.Register("settings", typeof(SettingsPage));
        _navigation.Register("detail", typeof(TrackDetailPage));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string tag)
        {
            _navigation.Navigate(tag);
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) =>
        _navigation.GoBack();

    private void OnNavigated(object? sender, string key)
    {
        NavView.IsBackEnabled = _navigation.CanGoBack;

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var match = NavView.MenuItems.Concat(NavView.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => (item.Tag as string) == key);

        if (match is not null)
        {
            NavView.SelectedItem = match;
        }
    }

    private void ClipboardBar_CloseButtonClick(InfoBar sender, object args) =>
        _shell.DismissClipboardLinkCommand.Execute(null);

    private async void RunStartupChecksAsync()
    {
        try
        {
            var status = await _toolchain.RefreshAsync().ConfigureAwait(true);
            _shell.ToolsMissing = !status.IsReady;

            if (_settings.Current.CheckForUpdatesOnStartup)
            {
                var result = await _updates.CheckAsync().ConfigureAwait(true);
                _shell.ApplyUpdateResult(result);
            }
        }
        catch (Exception)
        {
            // Non-fatal: the banners simply stay hidden.
        }
    }

    private void ApplyTheme(AppThemeMode mode)
    {
        RootGrid.RequestedTheme = mode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (AppWindow?.TitleBar is { } titleBar)
        {
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
    }

    private void ResizeToDefault()
    {
        try
        {
            AppWindow?.Resize(new SizeInt32(1220, 840));
        }
        catch (Exception)
        {
            // Resize is best-effort on constrained displays.
        }
    }
}

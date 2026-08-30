using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Helpers;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

/// <summary>Backs the persistent chrome: clipboard prompt, update banner, missing-tools banner.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardLink))]
    private string? _clipboardLink;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string? _updateVersion;

    [ObservableProperty]
    private string? _updateUrl;

    [ObservableProperty]
    private bool _toolsMissing;

    public ShellViewModel(INavigationService navigation, ISettingsService settings, IClipboardService clipboard)
    {
        _navigation = navigation;
        _settings = settings;
        clipboard.SupportedLinkDetected += (_, link) => RunOnUi(() =>
        {
            if (_settings.Current.AutoPasteClipboardLinks)
            {
                ClipboardLink = link;
            }
        });
    }

    public bool HasClipboardLink => !string.IsNullOrWhiteSpace(ClipboardLink);

    public string UpdateBannerText =>
        UpdateVersion is null ? "Une mise à jour est disponible." : $"WaveDL {UpdateVersion} est disponible.";

    partial void OnUpdateVersionChanged(string? value) => OnPropertyChanged(nameof(UpdateBannerText));

    public void ApplyUpdateResult(UpdateCheckResult result)
    {
        UpdateAvailable = result.UpdateAvailable;
        UpdateVersion = result.LatestVersion;
        UpdateUrl = result.ReleaseUrl;
    }

    [RelayCommand]
    private void UseClipboardLink()
    {
        var link = ClipboardLink;
        ClipboardLink = null;
        if (!string.IsNullOrWhiteSpace(link))
        {
            _navigation.Navigate("link", link);
        }
    }

    [RelayCommand]
    private void DismissClipboardLink() => ClipboardLink = null;

    [RelayCommand]
    private void OpenRelease()
    {
        if (!string.IsNullOrWhiteSpace(UpdateUrl))
        {
            SystemLauncher.OpenUrl(UpdateUrl);
        }
    }

    [RelayCommand]
    private void OpenToolSettings() => _navigation.Navigate("settings");
}

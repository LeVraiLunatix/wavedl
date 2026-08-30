using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Models;
using WaveDL.Services;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class LinkImportViewModel : ViewModelBase
{
    private readonly IMetadataService _metadata;
    private readonly ISearchService _search;
    private readonly IDownloadService _downloads;
    private readonly IClipboardService _clipboard;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _linkText = string.Empty;

    [ObservableProperty]
    private ExternalTrackInfo? _resolvedInfo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadPlaylistCommand))]
    private PlaylistInfo? _playlist;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedCommand))]
    private MatchCandidate? _selectedCandidate;

    [ObservableProperty]
    private AudioFormatOption _selectedFormat;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _hasResult;

    public LinkImportViewModel(
        IMetadataService metadata,
        ISearchService search,
        IDownloadService downloads,
        ISettingsService settings,
        IClipboardService clipboard,
        INavigationService navigation)
    {
        _metadata = metadata;
        _search = search;
        _downloads = downloads;
        _clipboard = clipboard;
        _navigation = navigation;
        _selectedFormat = AudioFormatOption.From(settings.Current.PreferredFormat);
    }

    public IReadOnlyList<AudioFormatOption> Formats => AudioFormatOption.All;

    public ObservableCollection<MatchCandidate> Candidates { get; } = [];

    public ObservableCollection<Track> PlaylistTracks { get; } = [];

    public bool ShowCandidates => Candidates.Count > 0;

    public bool ShowPlaylist => Playlist is not null;

    public bool ShowExternalInfo => ResolvedInfo is not null;

    public void Initialize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        LinkText = url;
        _ = ResolveAsync();
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        var text = await _clipboard.GetTextAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text))
        {
            LinkText = text.Trim();
        }
    }

    [RelayCommand]
    private async Task ResolveAsync()
    {
        ResetResult();
        var input = LinkText.Trim();
        if (input.Length == 0)
        {
            return;
        }

        var kind = _metadata.Classify(input);
        if (kind == ExternalLinkKind.NotAUrl)
        {
            ErrorMessage = "Collez un lien YouTube, YouTube Music, Spotify, Deezer ou Apple Music.";
            return;
        }

        await RunGuardedAsync(async () =>
        {
            switch (kind)
            {
                case ExternalLinkKind.Spotify:
                case ExternalLinkKind.Deezer:
                case ExternalLinkKind.AppleMusic:
                    await ResolveExternalAsync(input).ConfigureAwait(true);
                    break;

                case ExternalLinkKind.YouTubePlaylist:
                    await ResolvePlaylistAsync(input).ConfigureAwait(true);
                    break;

                default:
                    await ResolveDirectAsync(input).ConfigureAwait(true);
                    break;
            }
        }, "Résolution du lien impossible");

        RefreshVisibility();
    }

    private async Task ResolveExternalAsync(string input)
    {
        StatusText = "Identification du morceau…";
        var info = await _metadata.ResolveExternalAsync(input).ConfigureAwait(true);
        if (info is null)
        {
            ErrorMessage = "Impossible d'identifier ce morceau à partir du lien.";
            return;
        }

        ResolvedInfo = info;
        StatusText = "Recherche de la version YouTube Music…";

        var matches = await _search.FindMatchesAsync(info).ConfigureAwait(true);
        Candidates.Clear();
        foreach (var match in matches)
        {
            Candidates.Add(match);
        }

        SelectedCandidate = Candidates.FirstOrDefault();
        StatusText = Candidates.Count == 0
            ? "Aucune correspondance trouvée sur YouTube Music."
            : $"{Candidates.Count} correspondance(s) — la meilleure est présélectionnée.";
        HasResult = true;
    }

    private async Task ResolvePlaylistAsync(string input)
    {
        StatusText = "Lecture de la playlist…";
        var playlist = await _search.ResolvePlaylistAsync(input).ConfigureAwait(true);
        if (playlist is null || playlist.Count == 0)
        {
            ErrorMessage = "Playlist introuvable ou vide.";
            return;
        }

        Playlist = playlist;
        PlaylistTracks.Clear();
        foreach (var track in playlist.Tracks)
        {
            PlaylistTracks.Add(track);
        }

        StatusText = $"{playlist.Count} pistes prêtes à être téléchargées.";
        HasResult = true;
    }

    private async Task ResolveDirectAsync(string input)
    {
        StatusText = "Lecture du morceau…";
        var track = await _search.ResolveTrackAsync(input).ConfigureAwait(true);
        if (track is null)
        {
            ErrorMessage = "Ce lien n'a pas pu être lu.";
            return;
        }

        Candidates.Clear();
        Candidates.Add(new MatchCandidate { Track = track, Confidence = 1 });
        SelectedCandidate = Candidates[0];
        StatusText = "Morceau prêt à être téléchargé.";
        HasResult = true;
    }

    [RelayCommand(CanExecute = nameof(CanDownloadSelected))]
    private void DownloadSelected()
    {
        var candidate = SelectedCandidate;
        if (candidate is null)
        {
            return;
        }

        _downloads.Enqueue(new DownloadRequest
        {
            Url = candidate.Track.SourceUrl,
            Format = SelectedFormat.Value,
            DisplayTitle = ResolvedInfo?.Title ?? candidate.Track.Title,
            DisplayArtist = ResolvedInfo?.Artist ?? candidate.Track.Artist,
            ThumbnailUrl = ResolvedInfo?.CoverUrl ?? candidate.Track.BestThumbnailUrl,
        });

        _navigation.Navigate("downloads");
    }

    [RelayCommand(CanExecute = nameof(CanDownloadPlaylist))]
    private void DownloadPlaylist()
    {
        if (Playlist is null)
        {
            return;
        }

        _downloads.Enqueue(new DownloadRequest
        {
            Url = Playlist.Url,
            Format = SelectedFormat.Value,
            DisplayTitle = Playlist.Title,
            ThumbnailUrl = Playlist.ThumbnailUrl,
            IsPlaylist = true,
        });

        _navigation.Navigate("downloads");
    }

    private bool CanDownloadSelected => SelectedCandidate is not null;

    private bool CanDownloadPlaylist => Playlist is { Count: > 0 };

    private void ResetResult()
    {
        ErrorMessage = null;
        StatusText = null;
        HasResult = false;
        ResolvedInfo = null;
        Playlist = null;
        SelectedCandidate = null;
        Candidates.Clear();
        PlaylistTracks.Clear();
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        OnPropertyChanged(nameof(ShowCandidates));
        OnPropertyChanged(nameof(ShowPlaylist));
        OnPropertyChanged(nameof(ShowExternalInfo));
    }
}

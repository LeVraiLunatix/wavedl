using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class TrackDetailViewModel : ViewModelBase
{
    private readonly ISearchService _search;
    private readonly IDownloadService _downloads;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private Track? _track;

    [ObservableProperty]
    private AudioFormatOption _selectedFormat;

    [ObservableProperty]
    private string _bestQualityText = "Meilleure source disponible (automatique)";

    [ObservableProperty]
    private bool _streamsLoaded;

    public TrackDetailViewModel(
        ISearchService search,
        IDownloadService downloads,
        ISettingsService settings,
        INavigationService navigation)
    {
        _search = search;
        _downloads = downloads;
        _navigation = navigation;
        _selectedFormat = AudioFormatOption.From(settings.Current.PreferredFormat);
    }

    public IReadOnlyList<AudioFormatOption> Formats => AudioFormatOption.All;

    public ObservableCollection<AudioStreamInfo> Streams { get; } = [];

    public async Task LoadAsync(Track track)
    {
        Track = track;
        Streams.Clear();
        StreamsLoaded = false;

        await RunGuardedAsync(async () =>
        {
            var streams = await _search.GetAudioStreamsAsync(track.SourceUrl).ConfigureAwait(true);
            foreach (var stream in streams)
            {
                Streams.Add(stream);
            }

            if (streams.Count > 0)
            {
                BestQualityText = $"Meilleure source : {streams[0].DisplayName} — convertie en {SelectedFormat.Label}";
            }

            StreamsLoaded = true;
        }, "Analyse des qualités impossible");
    }

    [RelayCommand]
    private void Download()
    {
        if (Track is null)
        {
            return;
        }

        _downloads.Enqueue(new DownloadRequest
        {
            Url = Track.SourceUrl,
            Format = SelectedFormat.Value,
            DisplayTitle = Track.Title,
            DisplayArtist = Track.Artist,
            ThumbnailUrl = Track.BestThumbnailUrl,
        });

        _navigation.Navigate("downloads");
    }

    [RelayCommand]
    private void GoBack() => _navigation.GoBack();
}

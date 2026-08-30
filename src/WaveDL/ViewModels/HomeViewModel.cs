using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly ISearchService _search;
    private readonly IHistoryService _history;
    private readonly INavigationService _navigation;

    private CancellationTokenSource? _suggestionCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasRecent;

    public HomeViewModel(ISearchService search, IHistoryService history, INavigationService navigation)
    {
        _search = search;
        _history = history;
        _navigation = navigation;
        _history.Changed += (_, _) => _ = LoadRecentAsync();
    }

    public ObservableCollection<Track> Suggestions { get; } = [];

    public ObservableCollection<HistoryEntry> RecentDownloads { get; } = [];

    public async Task LoadRecentAsync()
    {
        var recent = await _history.GetRecentAsync(6).ConfigureAwait(true);
        RunOnUi(() =>
        {
            RecentDownloads.Clear();
            foreach (var entry in recent)
            {
                RecentDownloads.Add(entry);
            }

            HasRecent = RecentDownloads.Count > 0;
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        _suggestionCts?.Cancel();
        _suggestionCts = new CancellationTokenSource();
        _ = UpdateSuggestionsAsync(value, _suggestionCts.Token);
    }

    private async Task UpdateSuggestionsAsync(string query, CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (query.Length < 3 || LinkClassifier.IsUrl(query))
        {
            RunOnUi(Suggestions.Clear);
            return;
        }

        try
        {
            await Task.Delay(400, cancellationToken).ConfigureAwait(true);
            var results = await _search.SearchAsync(query, 6, cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUi(() =>
            {
                Suggestions.Clear();
                foreach (var track in results)
                {
                    Suggestions.Add(track);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            RunOnUi(Suggestions.Clear);
        }
    }

    [RelayCommand]
    private void Submit(string? queryText)
    {
        var text = (queryText ?? SearchText).Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (LinkClassifier.IsSupported(text))
        {
            _navigation.Navigate("link", text);
        }
        else
        {
            _navigation.Navigate("search", text);
        }
    }

    [RelayCommand]
    private void OpenSuggestion(Track? track)
    {
        if (track is not null)
        {
            _navigation.Navigate("detail", track);
        }
    }

    [RelayCommand]
    private void OpenRecent(HistoryEntry? entry)
    {
        if (entry is not null)
        {
            SystemLauncher.RevealInExplorer(entry.FilePath);
        }
    }

    [RelayCommand]
    private void GoToSearch() => _navigation.Navigate("search");

    [RelayCommand]
    private void GoToLink() => _navigation.Navigate("link");

    [RelayCommand]
    private void GoToHistory() => _navigation.Navigate("history");
}

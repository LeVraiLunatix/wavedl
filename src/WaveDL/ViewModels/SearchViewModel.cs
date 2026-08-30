using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class SearchViewModel : ViewModelBase
{
    private readonly ISearchService _search;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _hasSearched;

    public SearchViewModel(ISearchService search, INavigationService navigation)
    {
        _search = search;
        _navigation = navigation;
    }

    public ObservableCollection<Track> Results { get; } = [];

    public bool ShowEmptyState => HasSearched && !IsBusy && Results.Count == 0;

    public void Initialize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == Query)
        {
            return;
        }

        Query = query;
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            var results = await _search.SearchAsync(query, 30).ConfigureAwait(true);
            Results.Clear();
            foreach (var track in results)
            {
                Results.Add(track);
            }

            HasSearched = true;
        }, "Recherche impossible");

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand]
    private void OpenTrack(Track? track)
    {
        if (track is not null)
        {
            _navigation.Navigate("detail", track);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(HasSearched))
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowEmptyState)));
        }
    }
}

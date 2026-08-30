using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using WaveDL.Models;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class SearchPage : Page
{
    public SearchPage()
    {
        ViewModel = App.GetService<SearchViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public SearchViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string query)
        {
            ViewModel.Initialize(query);
        }
    }

    private void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private void OnResultClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Track track)
        {
            return;
        }

        try
        {
            ResultsList.PrepareConnectedAnimation("trackCover", track, "CoverImage");
        }
        catch
        {
            // Connected animation is a nicety, never a requirement.
        }

        ViewModel.OpenTrackCommand.Execute(track);
    }
}

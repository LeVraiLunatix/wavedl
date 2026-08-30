using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WaveDL.Models;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        ViewModel = App.GetService<HomeViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public HomeViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadRecentAsync();
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Track track)
        {
            ViewModel.OpenSuggestionCommand.Execute(track);
        }
        else
        {
            ViewModel.SubmitCommand.Execute(args.QueryText);
        }
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Track track)
        {
            ViewModel.OpenSuggestionCommand.Execute(track);
        }
    }

    private void OnRecentClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HistoryEntry entry)
        {
            ViewModel.OpenRecentCommand.Execute(entry);
        }
    }
}

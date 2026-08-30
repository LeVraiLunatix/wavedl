using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WaveDL.Models;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        ViewModel = App.GetService<HistoryViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public HistoryViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HistoryEntry entry })
        {
            ViewModel.OpenFolderCommand.Execute(entry);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HistoryEntry entry })
        {
            ViewModel.DeleteCommand.Execute(entry);
        }
    }

    private async void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Vider l'historique",
            Content = "Cette action supprime toutes les entrées de l'historique. Les fichiers téléchargés ne sont pas supprimés.",
            PrimaryButtonText = "Vider",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.ClearAllCommand.Execute(null);
        }
    }
}

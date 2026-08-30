using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public SettingsViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}

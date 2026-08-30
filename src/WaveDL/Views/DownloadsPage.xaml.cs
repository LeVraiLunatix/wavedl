using Microsoft.UI.Xaml.Controls;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class DownloadsPage : Page
{
    public DownloadsPage()
    {
        ViewModel = App.GetService<DownloadsViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public DownloadsViewModel ViewModel { get; }
}

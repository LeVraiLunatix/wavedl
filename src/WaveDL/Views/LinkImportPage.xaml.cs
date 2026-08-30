using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class LinkImportPage : Page
{
    public LinkImportPage()
    {
        ViewModel = App.GetService<LinkImportViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public LinkImportViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string url)
        {
            ViewModel.Initialize(url);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text) || e.DataView.Contains(StandardDataFormats.WebLink))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Analyser ce lien";
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            string? link = null;
            if (e.DataView.Contains(StandardDataFormats.WebLink))
            {
                link = (await e.DataView.GetWebLinkAsync()).ToString();
            }
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                link = await e.DataView.GetTextAsync();
            }

            if (!string.IsNullOrWhiteSpace(link))
            {
                ViewModel.Initialize(link.Trim());
            }
        }
        finally
        {
            deferral.Complete();
        }
    }
}

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WaveDL.Models;
using WaveDL.ViewModels;

namespace WaveDL.Views;

public sealed partial class TrackDetailPage : Page
{
    public TrackDetailPage()
    {
        ViewModel = App.GetService<TrackDetailViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    public TrackDetailViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        TryStartCoverAnimation();

        if (e.Parameter is Track track)
        {
            await ViewModel.LoadAsync(track);
        }
    }

    private void TryStartCoverAnimation()
    {
        try
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("trackCover");
            if (animation is not null)
            {
                animation.Configuration = new DirectConnectedAnimationConfiguration();
                animation.TryStart(DetailCover);
            }
        }
        catch
        {
            // The page is fully functional without the shared-element transition.
        }
    }
}

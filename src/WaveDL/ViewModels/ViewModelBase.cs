using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace WaveDL.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected DispatcherQueue Dispatcher { get; } = DispatcherQueue.GetForCurrentThread();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Marshals <paramref name="action"/> onto the UI thread.</summary>
    protected void RunOnUi(Action action)
    {
        if (Dispatcher is null || Dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            Dispatcher.TryEnqueue(() => action());
        }
    }

    /// <summary>Runs <paramref name="work"/> with busy state and uniform error capture.</summary>
    protected async Task RunGuardedAsync(Func<Task> work, string? failurePrefix = null)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Superseded or cancelled on purpose — not an error.
        }
        catch (Exception ex)
        {
            ErrorMessage = failurePrefix is null ? ex.Message : $"{failurePrefix} : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class DownloadsViewModel : ViewModelBase
{
    public DownloadsViewModel(IDownloadService downloads)
    {
        foreach (var handle in downloads.Handles)
        {
            Items.Add(new DownloadItemViewModel(handle));
        }

        downloads.DownloadAdded += (_, handle) =>
            RunOnUi(() => Items.Insert(0, new DownloadItemViewModel(handle)));

        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public bool HasFinishedItems => Items.Any(i => i.IsFinished);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasFinishedItems));
    }

    [RelayCommand]
    private void ClearFinished()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].IsFinished)
            {
                Items.RemoveAt(i);
            }
        }
    }
}

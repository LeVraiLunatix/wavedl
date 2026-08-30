using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryService _history;

    [ObservableProperty]
    private bool _isEmpty = true;

    public HistoryViewModel(IHistoryService history)
    {
        _history = history;
        _history.Changed += (_, _) => _ = LoadAsync();
    }

    public ObservableCollection<HistoryEntry> Entries { get; } = [];

    public async Task LoadAsync()
    {
        await RunGuardedAsync(async () =>
        {
            var entries = await _history.GetAllAsync().ConfigureAwait(true);
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            IsEmpty = Entries.Count == 0;
        }, "Chargement de l'historique impossible");
    }

    [RelayCommand]
    private void OpenFolder(HistoryEntry? entry)
    {
        if (entry is not null)
        {
            SystemLauncher.RevealInExplorer(entry.FilePath);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(HistoryEntry? entry)
    {
        if (entry is not null)
        {
            await _history.DeleteAsync(entry.Id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ClearAllAsync() => await _history.ClearAsync().ConfigureAwait(true);
}

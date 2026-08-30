using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaveDL.Helpers;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.ViewModels;

public sealed partial class DownloadItemViewModel : ViewModelBase
{
    private readonly IDownloadHandle _handle;
    private DownloadProgress _progress;
    private string? _primaryFile;

    public DownloadItemViewModel(IDownloadHandle handle)
    {
        _handle = handle;
        _progress = handle.Progress;
        handle.ProgressChanged += OnProgressChanged;
        _ = TrackCompletionAsync();
    }

    public string Title => string.IsNullOrWhiteSpace(_handle.Request.DisplayTitle)
        ? "Téléchargement"
        : _handle.Request.DisplayTitle!;

    public string Artist => _handle.Request.DisplayArtist ?? string.Empty;

    public string? ThumbnailUrl => _handle.Request.ThumbnailUrl;

    public DownloadState State => _progress.State;

    public double ProgressValue => Math.Clamp(_progress.Percent, 0, 100);

    public string PercentText => $"{ProgressValue:0} %";

    public string FormatText => _handle.Request.Format.Describe();

    public string SpeedText => Humanize.Speed(_progress.SpeedBytesPerSecond);

    public string EtaText => Humanize.Eta(_progress.Eta);

    public string SizeText => _progress.TotalBytes > 0
        ? $"{Humanize.Bytes(_progress.DownloadedBytes)} / {Humanize.Bytes(_progress.TotalBytes)}"
        : Humanize.Bytes(_progress.DownloadedBytes);

    public string StatusText => _progress.StatusText ?? StateLabel;

    public string StateLabel => State switch
    {
        DownloadState.Queued => "En file d'attente",
        DownloadState.Preparing => "Préparation",
        DownloadState.Downloading => "Téléchargement",
        DownloadState.Converting => "Conversion",
        DownloadState.Paused => "En pause",
        DownloadState.Completed => "Terminé",
        DownloadState.Failed => "Échec",
        DownloadState.Canceled => "Annulé",
        _ => string.Empty,
    };

    public bool IsActive => State is DownloadState.Queued or DownloadState.Preparing
        or DownloadState.Downloading or DownloadState.Converting or DownloadState.Paused;

    public bool IsFinished => State is DownloadState.Completed or DownloadState.Failed or DownloadState.Canceled;

    public bool IsCompleted => State == DownloadState.Completed;

    public bool IsFailed => State == DownloadState.Failed;

    public bool IsIndeterminate => State is DownloadState.Preparing or DownloadState.Converting;

    public bool ShowProgressBar => IsActive;

    public bool ShowMetrics => State is DownloadState.Downloading;

    public bool IsPaused => State == DownloadState.Paused;

    public string PauseResumeLabel => State == DownloadState.Paused ? "Reprendre" : "Suspendre";

    private void OnProgressChanged(object? sender, DownloadProgress progress) => RunOnUi(() =>
    {
        _progress = progress;
        RaiseAll();
    });

    private async Task TrackCompletionAsync()
    {
        try
        {
            var result = await _handle.Completion.ConfigureAwait(true);
            _primaryFile = result.PrimaryFile;
        }
        catch
        {
            // The failed/cancelled state is already carried by the progress snapshot.
        }
        finally
        {
            RunOnUi(RaiseAll);
        }
    }

    private void RaiseAll()
    {
        OnPropertyChanged(string.Empty);
        PauseResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private void PauseResume()
    {
        if (State == DownloadState.Paused)
        {
            _handle.Resume();
        }
        else
        {
            _handle.Pause();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _handle.Cancel();

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (!string.IsNullOrWhiteSpace(_primaryFile))
        {
            SystemLauncher.RevealInExplorer(_primaryFile);
        }
    }

    private bool CanPauseResume => State is DownloadState.Downloading or DownloadState.Preparing
        or DownloadState.Converting or DownloadState.Paused;

    private bool CanCancel => IsActive;

    private bool CanOpenFolder => IsCompleted && !string.IsNullOrWhiteSpace(_primaryFile);
}

using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

/// <summary>
/// Watches the Windows clipboard and raises <see cref="SupportedLinkDetected"/> when a
/// YouTube / Spotify / Deezer / Apple Music link appears. All clipboard access is marshalled
/// to the UI thread.
/// </summary>
public sealed class ClipboardService(ILogger<ClipboardService> logger) : IClipboardService
{
    private DispatcherQueue? _dispatcher;
    private string? _lastSeen;
    private bool _started;

    public event EventHandler<string>? SupportedLinkDetected;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Clipboard.ContentChanged += OnClipboardContentChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        Clipboard.ContentChanged -= OnClipboardContentChanged;
        _started = false;
    }

    public async Task<string?> GetTextAsync()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.Text))
            {
                return null;
            }

            return await view.GetTextAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Lecture du presse-papiers impossible.");
            return null;
        }
    }

    private void OnClipboardContentChanged(object? sender, object e)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            try
            {
                var text = await GetTextAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var trimmed = text.Trim();
                if (trimmed == _lastSeen || !LinkClassifier.IsSupported(trimmed))
                {
                    return;
                }

                _lastSeen = trimmed;
                SupportedLinkDetected?.Invoke(this, trimmed);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Traitement d'un changement de presse-papiers impossible.");
            }
        });
    }
}

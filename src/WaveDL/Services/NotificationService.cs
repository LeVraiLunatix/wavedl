using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class NotificationService(
    ISettingsService settings,
    ILogger<NotificationService> logger) : INotificationService
{
    private bool _registered;

    public void TryRegister()
    {
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
            logger.LogInformation("Gestionnaire de notifications enregistré.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Enregistrement des notifications Windows impossible.");
        }
    }

    public void NotifyDownloadCompleted(string title, string artist, string filePath)
    {
        var subtitle = string.IsNullOrWhiteSpace(artist) ? "Téléchargement terminé" : $"{artist} — téléchargement terminé";
        Show(builder => builder
            .AddText(title)
            .AddText(subtitle)
            .SetAttributionText("WaveDL"));
    }

    public void NotifyDownloadFailed(string title, string reason)
    {
        Show(builder => builder
            .AddText($"Échec : {title}")
            .AddText(string.IsNullOrWhiteSpace(reason) ? "Le téléchargement a échoué." : reason)
            .SetAttributionText("WaveDL"));
    }

    public void NotifyInformation(string title, string message)
    {
        Show(builder => builder
            .AddText(title)
            .AddText(message)
            .SetAttributionText("WaveDL"));
    }

    private void Show(Action<AppNotificationBuilder> configure)
    {
        if (!_registered || !settings.Current.ShowNotifications)
        {
            return;
        }

        try
        {
            var builder = new AppNotificationBuilder();
            configure(builder);
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Affichage d'une notification impossible.");
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private AppSettingsModel _current = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BinDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public AppSettingsModel Current => _current;

    public string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WaveDL");

    public string BinDirectory => Path.Combine(DataDirectory, "bin");

    public string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public string DatabasePath => Path.Combine(DataDirectory, "history.db");

    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public string DefaultDownloadDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "WaveDL");

    public string EffectiveDownloadDirectory
    {
        get
        {
            var target = string.IsNullOrWhiteSpace(_current.DownloadDirectory)
                ? DefaultDownloadDirectory
                : _current.DownloadDirectory;

            try
            {
                Directory.CreateDirectory(target);
                return target;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _logger.LogWarning(ex, "Dossier de téléchargement invalide ({Target}), repli sur le dossier par défaut.", target);
                Directory.CreateDirectory(DefaultDownloadDirectory);
                return DefaultDownloadDirectory;
            }
        }
    }

    public event EventHandler<AppSettingsModel>? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettingsModel>(json, JsonOptions);
                if (loaded is not null)
                {
                    _current = Sanitize(loaded);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Lecture des paramètres impossible, utilisation des valeurs par défaut.");
        }

        _current = new AppSettingsModel();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _current = Sanitize(_current);

        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_current, JsonOptions);
            var temp = SettingsPath + ".tmp";
            await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Échec de l'enregistrement des paramètres.");
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke(this, _current);
    }

    private static AppSettingsModel Sanitize(AppSettingsModel model)
    {
        model.MaxParallelDownloads = Math.Clamp(model.MaxParallelDownloads, 1, 8);
        model.RateLimitKbps = Math.Max(0, model.RateLimitKbps);
        return model;
    }
}

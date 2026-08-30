using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class UpdateService(
    IHttpClientFactory httpClientFactory,
    ILogger<UpdateService> logger) : IUpdateService
{
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = AppInfo.VersionText;

        try
        {
            using var client = httpClientFactory.CreateClient("default");
            using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.ReleasesApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(AppInfo.UserAgent);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(false, current, null, null, null);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;
            var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(tag))
            {
                return new UpdateCheckResult(false, current, null, htmlUrl, body);
            }

            var latestText = tag.TrimStart('v', 'V');
            var updateAvailable = Version.TryParse(latestText, out var latest)
                && Version.TryParse(current, out var currentVersion)
                && latest > currentVersion;

            return new UpdateCheckResult(updateAvailable, current, latestText, htmlUrl, body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vérification des mises à jour impossible.");
            return new UpdateCheckResult(false, current, null, null, null);
        }
    }
}

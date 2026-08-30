using Microsoft.Extensions.Logging;
using WaveDL.Models;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class MetadataService(
    IEnumerable<IExternalMetadataProvider> providers,
    ILogger<MetadataService> logger) : IMetadataService
{
    private readonly IReadOnlyList<IExternalMetadataProvider> _providers = providers.ToList();

    public ExternalLinkKind Classify(string input) => LinkClassifier.Classify(input);

    public async Task<ExternalTrackInfo?> ResolveExternalAsync(string url, CancellationToken cancellationToken = default)
    {
        var kind = LinkClassifier.Classify(url);
        var provider = _providers.FirstOrDefault(p => p.CanHandle(kind));
        if (provider is null)
        {
            logger.LogWarning("Aucun fournisseur de métadonnées pour le type {Kind}.", kind);
            return null;
        }

        return await provider.ResolveAsync(url, cancellationToken).ConfigureAwait(false);
    }
}

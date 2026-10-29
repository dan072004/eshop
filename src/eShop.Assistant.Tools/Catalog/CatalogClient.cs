using System.Net.Http.Json;

namespace eShop.Assistant.Tools.Catalog;

/// <summary>Typed client for Catalog.API v2, addressed by its Aspire service name. It needs no token.</summary>
public sealed class CatalogClient(HttpClient httpClient)
{
    public const string BaseAddress = "https+http://catalog-api";

    /// <exception cref="HttpRequestException">Catalog.API could not be reached or returned an error status.</exception>
    public async Task<CatalogSearchResult> SearchBySemanticRelevanceAsync(
        string text, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var uri = $"api/catalog/items/withsemanticrelevance?text={Uri.EscapeDataString(text)}&pageIndex={pageIndex}&pageSize={pageSize}";
        var page = await httpClient.GetFromJsonAsync<CatalogSearchResult>(uri, cancellationToken);
        return page ?? throw new HttpRequestException("Catalog.API returned an empty body.");
    }
}

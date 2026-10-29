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

    /// <summary>The catalog items with these ids; ids Catalog doesn't know are simply absent.</summary>
    /// <exception cref="HttpRequestException">Catalog.API could not be reached or returned an error status.</exception>
    public async Task<IReadOnlyList<CatalogItemRef>> GetItemsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        var items = await httpClient.GetFromJsonAsync<List<CatalogItemRef>>($"api/catalog/items/by?{query}", cancellationToken);
        return items ?? throw new HttpRequestException("Catalog.API returned an empty body.");
    }

    /// <exception cref="HttpRequestException">Catalog.API could not be reached or returned an error status.</exception>
    public async Task<IReadOnlyList<CatalogTypeInfo>> GetTypesAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<CatalogTypeInfo>>("api/catalog/catalogtypes", cancellationToken)
            ?? throw new HttpRequestException("Catalog.API returned an empty body.");

    /// <exception cref="HttpRequestException">Catalog.API could not be reached or returned an error status.</exception>
    public async Task<IReadOnlyList<CatalogBrandInfo>> GetBrandsAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<CatalogBrandInfo>>("api/catalog/catalogbrands", cancellationToken)
            ?? throw new HttpRequestException("Catalog.API returned an empty body.");
}

/// <summary>A catalog item's id and the ids of its type and brand (Catalog.API's batch endpoint sends no names).</summary>
public sealed record CatalogItemRef(int Id, int CatalogTypeId, int CatalogBrandId);

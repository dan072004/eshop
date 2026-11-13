using System.Net.Http.Json;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// Catalog.API for the fixed world, at the tools library's outbound <see cref="HttpMessageHandler"/>. It answers the
/// item, type and brand lookups <c>get_order</c> makes for the world's products, and answers <c>search_catalog</c>
/// with the results the current case lists (or none), in Catalog.API's shapes.
/// </summary>
/// <remarks>A canned-answer stub at a boundary, so it has no contract test.</remarks>
public sealed class CatalogStub : HttpMessageHandler
{
    private const string DefaultPageSize = "10";
    private const int AvailableStock = 100;

    private readonly IReadOnlyList<WorldProduct> _products;
    private readonly IReadOnlyList<WorldProduct> _searchResults;
    private readonly List<string> _types;
    private readonly List<string> _brands;

    public CatalogStub(EvalWorld world, IReadOnlyList<WorldProduct> searchResults)
    {
        _searchResults = searchResults;
        _products = [.. world.Products, .. searchResults.Where(r => world.Products.All(p => p.Id != r.Id))];

        // Catalog numbers its types and brands itself; the stub numbers the ones it knows, in a stable order.
        _types = [.. _products.Select(p => p.Type).Distinct().Order(StringComparer.Ordinal)];
        _brands = [.. _products.Select(p => p.Brand).Distinct().Order(StringComparer.Ordinal)];
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        return Task.FromResult(uri.AbsolutePath.TrimEnd('/') switch
        {
            "/api/catalog/items/withsemanticrelevance" => Ok(SearchPage(int.Parse(query["pageSize"] ?? DefaultPageSize))),
            "/api/catalog/items/by" => Ok(ItemsByIds(query.GetValues("ids") ?? [])),
            "/api/catalog/catalogtypes" => Ok(_types.Select((type, i) => new { Id = i + 1, Type = type })),
            "/api/catalog/catalogbrands" => Ok(_brands.Select((brand, i) => new { Id = i + 1, Brand = brand })),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
    }

    // One v2 search page (Catalog.API's PaginatedItems<CatalogItem>).
    private object SearchPage(int pageSize) => new
    {
        PageIndex = 0,
        PageSize = pageSize,
        Count = (long)_searchResults.Count,
        Data = _searchResults.Take(pageSize).Select(Item),
    };

    // Like Catalog.API's batch endpoint: ids it doesn't know are left out.
    private IEnumerable<object> ItemsByIds(IEnumerable<string> ids) =>
        ids.Select(int.Parse).Distinct()
            .Select(id => _products.SingleOrDefault(p => p.Id == id))
            .OfType<WorldProduct>()
            .Select(Item);

    // A CatalogItem as Catalog.API sends it: ids for type and brand, navigation properties not loaded.
    private object Item(WorldProduct product) => new
    {
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        PictureFileName = $"{product.Id}.webp",
        CatalogTypeId = _types.IndexOf(product.Type) + 1,
        CatalogType = (object?)null,
        CatalogBrandId = _brands.IndexOf(product.Brand) + 1,
        CatalogBrand = (object?)null,
        AvailableStock,
    };

    private static HttpResponseMessage Ok(object body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: JsonSerializerOptions.Web) };
}

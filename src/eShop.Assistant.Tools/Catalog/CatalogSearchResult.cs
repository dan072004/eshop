using System.Text.Json.Serialization;

namespace eShop.Assistant.Tools.Catalog;

/// <summary>The result of <c>search_catalog</c>: one page of catalog items, most relevant first.</summary>
public sealed record CatalogSearchResult(int PageIndex, int PageSize, long Count, IReadOnlyList<CatalogSearchItem> Data);

public sealed record CatalogSearchItem(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PictureUrl,
    int CatalogBrandId,
    CatalogBrandInfo? CatalogBrand,
    int CatalogTypeId,
    CatalogTypeInfo? CatalogType);

public sealed record CatalogBrandInfo(int Id, string Brand);

public sealed record CatalogTypeInfo(int Id, string Type);

using eShop.WebApp.Services;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// The basket for the evals, which leave basket tools out of scope. It stands behind both services the conversation
/// class's basket tools call (<c>AddToCart</c> looks the item up first), and every call throws, so the conversation
/// returns its "Unable to…" tool result. A case that doesn't expect a basket call counts one against itself.
/// </summary>
public sealed class OutOfScopeBasket : IBasketState, ICatalogService
{
    public Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync() => throw OutOfScope();

    public Task AddAsync(CatalogItem item) => throw OutOfScope();

    public Task<CatalogItem?> GetCatalogItem(int id) => throw OutOfScope();

    public Task<CatalogResult> GetCatalogItems(int pageIndex, int pageSize, int[]? brands, int[]? types) => throw OutOfScope();

    public Task<List<CatalogItem>> GetCatalogItems(IEnumerable<int> ids) => throw OutOfScope();

    public Task<CatalogResult> GetCatalogItemsWithSemanticRelevance(int page, int take, string text) => throw OutOfScope();

    public Task<IEnumerable<CatalogBrand>> GetBrands() => throw OutOfScope();

    public Task<IEnumerable<CatalogItemType>> GetTypes() => throw OutOfScope();

    private static InvalidOperationException OutOfScope() => new("Basket tools are out of the evals' scope.");
}

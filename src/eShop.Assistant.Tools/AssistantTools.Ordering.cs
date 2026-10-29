using System.ComponentModel;
using System.Net;
using eShop.Assistant.Tools.Ordering;

namespace eShop.Assistant.Tools;

// The order tools. They add no ownership logic: Ordering.API's endpoints decide whose orders come back.
public sealed partial class AssistantTools
{
    private const string SignInForOrdersMessage = "The Buyer must sign in to see their orders.";
    private const string OrderingUnavailableMessage = "Error accessing orders.";
    private const string OrderNotFoundMessage = "The Buyer has no order with that order number.";

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private Task<object> ListMyOrdersAsync(CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var orders = await _ordering.GetMyOrdersAsync(cancellationToken);
            return new MyOrdersResult(Today, [.. orders.Select(o => new MyOrder(o.OrderNumber, o.Date, o.Status, o.Total))]);
        }, OrderingUnavailableMessage, cancellationToken, KnownOrderingError);

    private async Task<object> GetOrderAsync(
        [Description("The order number, as list_my_orders returns it")] int orderNumber,
        CancellationToken cancellationToken)
    {
        // Any order number goes through: Ordering.API answers 404 for orders that aren't the Buyer's.
        var fetched = await GuardAsync(() => _ordering.GetOrderAsync(orderNumber, cancellationToken),
            OrderingUnavailableMessage, cancellationToken, KnownOrderingError);
        if (fetched is not OrderDetailsResponse order)
        {
            return fetched;
        }

        var lines = order.OrderItems ?? [];

        // Not degraded to null types: without them the model could apply the wrong per-type Return Window.
        var looked = await GuardAsync(
            () => GetTypesAndBrandsAsync(lines.Select(i => i.ProductId).Distinct().ToList(), cancellationToken),
            CatalogUnavailableMessage, cancellationToken);
        if (looked is not Dictionary<int, (string? Type, string? Brand)> catalog)
        {
            return looked;
        }

        return new OrderResult(Today, order.OrderNumber, order.Date, order.Status,
        [
            .. lines.Select(i =>
            {
                var (type, brand) = catalog.GetValueOrDefault(i.ProductId);
                return new OrderItemResult(i.ProductId, i.ProductName, i.Units, i.UnitPrice, type, brand);
            })
        ]);
    }

    // Type and brand names by product id. Products Catalog doesn't know are absent (their items get nulls).
    private async Task<Dictionary<int, (string? Type, string? Brand)>> GetTypesAndBrandsAsync(
        IReadOnlyList<int> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var itemsTask = _catalog.GetItemsByIdsAsync(productIds, cancellationToken);
        var typesTask = _catalog.GetTypesAsync(cancellationToken);
        var brandsTask = _catalog.GetBrandsAsync(cancellationToken);
        await Task.WhenAll(itemsTask, typesTask, brandsTask);

        var types = (await typesTask).ToDictionary(t => t.Id, t => t.Type);
        var brands = (await brandsTask).ToDictionary(b => b.Id, b => b.Brand);
        return (await itemsTask).ToDictionary(
            item => item.Id,
            item => (types.GetValueOrDefault(item.CatalogTypeId), brands.GetValueOrDefault(item.CatalogBrandId)));
    }

    // The Ordering.API failures that mean something to the Buyer; anything else is left to the guard (unavailable).
    private static ToolError? KnownOrderingError(Exception e) => e switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => ToolError.SignInRequired(SignInForOrdersMessage),
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => ToolError.NotFound(OrderNotFoundMessage),
        _ => null,
    };
}

using System.ComponentModel;
using System.Net;
using eShop.Assistant.Tools.Ordering;
using Microsoft.Extensions.Logging;

namespace eShop.Assistant.Tools;

// The order tools. They add no ownership logic: Ordering.API's endpoints decide whose orders come back.
public sealed partial class AssistantTools
{
    private const string SignInForOrdersMessage = "The Buyer must sign in to see their orders.";
    private const string OrderingUnavailableMessage = "Error accessing orders.";
    private const string OrderNotFoundMessage = "The Buyer has no order with that order number.";

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private async Task<object> ListMyOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var orders = await _ordering.GetMyOrdersAsync(cancellationToken);
            return new MyOrdersResult(Today, [.. orders.Select(o => new MyOrder(o.OrderNumber, o.Date, o.Status, o.Total))]);
        }
        catch (Exception e) when (OrderingError(e, cancellationToken) is { } error)
        {
            return error;
        }
    }

    private async Task<object> GetOrderAsync(
        [Description("The order number, as list_my_orders returns it")] int orderNumber,
        CancellationToken cancellationToken)
    {
        OrderingOrder order;
        try
        {
            // Any order number goes through: Ordering.API answers 404 for orders that aren't the Buyer's.
            order = await _ordering.GetOrderAsync(orderNumber, cancellationToken);
        }
        catch (Exception e) when (OrderingError(e, cancellationToken) is { } error)
        {
            return error;
        }

        var lines = order.OrderItems ?? [];
        Dictionary<int, (string? Type, string? Brand)> catalog;
        try
        {
            catalog = await GetTypesAndBrandsAsync(lines.Select(i => i.ProductId).Distinct().ToList(), cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Not degraded to null types: without them the model could apply the wrong per-type Return Window.
            const string Message = "Error accessing catalog.";
            _logger.LogError(e, Message);
            return ToolError.Unavailable(Message);
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

    // Maps an Ordering.API failure to tool output; null for the caller's own cancellation, which is rethrown.
    private ToolError? OrderingError(Exception e, CancellationToken cancellationToken)
    {
        if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (e is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
        {
            return ToolError.SignInRequired(SignInForOrdersMessage);
        }

        if (e is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
        {
            return ToolError.NotFound(OrderNotFoundMessage);
        }

        _logger.LogError(e, OrderingUnavailableMessage);
        return ToolError.Unavailable(OrderingUnavailableMessage);
    }
}

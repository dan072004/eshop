using System.Net;
using eShop.Assistant.Tools.Ordering;
using Microsoft.Extensions.Logging;

namespace eShop.Assistant.Tools;

// The order tools. They add no ownership logic: Ordering.API's endpoints decide whose orders come back.
public sealed partial class AssistantTools
{
    private const string SignInForOrdersMessage = "The Buyer must sign in to see their orders.";
    private const string OrderingUnavailableMessage = "Error accessing orders.";

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

        _logger.LogError(e, OrderingUnavailableMessage);
        return ToolError.Unavailable(OrderingUnavailableMessage);
    }
}

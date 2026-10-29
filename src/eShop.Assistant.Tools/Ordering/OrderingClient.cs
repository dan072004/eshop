using System.Net.Http.Json;

namespace eShop.Assistant.Tools.Ordering;

/// <summary>
/// Typed client for Ordering.API v1, addressed by its Aspire service name. Every call needs the Buyer's token, which
/// the host attaches with its own <see cref="DelegatingHandler"/> on <see cref="AssistantToolsBuilder.OrderingClient"/>;
/// this library has no auth code. Ordering.API decides whose orders come back.
/// </summary>
public sealed class OrderingClient(HttpClient httpClient)
{
    public const string BaseAddress = "https+http://ordering-api";

    /// <summary>The signed-in Buyer's orders, in the order Ordering.API returns them (newest-first).</summary>
    /// <exception cref="HttpRequestException">
    /// Ordering.API could not be reached or returned an error status; <see cref="HttpRequestException.StatusCode"/>
    /// is <see cref="System.Net.HttpStatusCode.Unauthorized"/> when there was no valid token.
    /// </exception>
    public async Task<IReadOnlyList<OrderingOrderSummary>> GetMyOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await httpClient.GetFromJsonAsync<List<OrderingOrderSummary>>("api/orders", cancellationToken);
        return orders ?? throw new HttpRequestException("Ordering.API returned an empty body.");
    }

    /// <summary>One order with its items. Ordering.API returns 404 for unknown orders and for other Buyers' orders.</summary>
    /// <exception cref="HttpRequestException">
    /// Ordering.API could not be reached or returned an error status; <see cref="HttpRequestException.StatusCode"/>
    /// is <see cref="System.Net.HttpStatusCode.NotFound"/> or <see cref="System.Net.HttpStatusCode.Unauthorized"/>.
    /// </exception>
    public async Task<OrderingOrder> GetOrderAsync(int orderNumber, CancellationToken cancellationToken)
    {
        var order = await httpClient.GetFromJsonAsync<OrderingOrder>($"api/orders/{orderNumber}", cancellationToken);
        return order ?? throw new HttpRequestException("Ordering.API returned an empty body.");
    }
}

/// <summary>One entry of Ordering.API's order list (its <c>OrderSummary</c>), as it is sent on the wire.</summary>
public sealed record OrderingOrderSummary(int OrderNumber, DateTime Date, string Status, decimal Total);

/// <summary>The parts of Ordering.API's <c>Order</c> the tools use, as it is sent on the wire.</summary>
public sealed record OrderingOrder(int OrderNumber, DateTime Date, string Status, IReadOnlyList<OrderingOrderItem>? OrderItems);

public sealed record OrderingOrderItem(int ProductId, string ProductName, int Units, decimal UnitPrice);

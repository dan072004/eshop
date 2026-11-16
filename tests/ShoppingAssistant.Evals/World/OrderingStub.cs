using System.Net.Http.Json;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// Ordering.API v1 for the fixed world, at the tools library's outbound <see cref="HttpMessageHandler"/>. It answers by
/// bearer token, which is the Buyer's user name (<c>alice</c>, <c>bob</c>) as <see cref="BuyerTokenHandler"/> sends it:
/// a Buyer's own orders newest first, any other order number 404 (unknown or not yours alike), no token 401. That is
/// Ordering.API's own behaviour, and the bodies are its <c>OrderSummary</c> and <c>Order</c> view models.
/// </summary>
/// <remarks>A canned-answer stub at a boundary, so it has no contract test.</remarks>
public sealed class OrderingStub(EvalWorld world) : HttpMessageHandler
{
    private const string OrdersPath = "/api/orders";
    private const string OrderPathPrefix = OrdersPath + "/";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var buyer = SignedInBuyer(request);
        if (buyer is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        var orders = world.Orders.Where(o => o.Buyer == buyer.UserName).OrderByDescending(o => o.OrderDate).ToList();
        var path = request.RequestUri!.AbsolutePath.TrimEnd('/');

        return Task.FromResult(path switch
        {
            OrdersPath => Ok(orders.Select(o => new { o.OrderNumber, Date = o.OrderDate, o.Status, Total = Total(o) })),
            _ when path.StartsWith(OrderPathPrefix, StringComparison.Ordinal)
                && int.TryParse(path[OrderPathPrefix.Length..], out var number)
                && orders.SingleOrDefault(o => o.OrderNumber == number) is { } order => Ok(Details(order, buyer)),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
    }

    private WorldBuyer? SignedInBuyer(HttpRequestMessage request) =>
        request.Headers.Authorization is { Scheme: "Bearer", Parameter: { } token }
            ? world.Buyers.SingleOrDefault(b => b.UserName == token)
            : null;

    private object Details(WorldOrder order, WorldBuyer buyer) => new
    {
        order.OrderNumber,
        Date = order.OrderDate,
        order.Status,
        Description = (string?)null,
        Street = buyer.Claims["address_street"],
        City = buyer.Claims["address_city"],
        State = buyer.Claims["address_state"],
        Zipcode = buyer.Claims["address_zip_code"],
        Country = buyer.Claims["address_country"],
        OrderItems = order.Items.Select(i =>
        {
            var product = world.Product(i.ProductId);
            return new { i.ProductId, ProductName = product.Name, i.Units, UnitPrice = product.Price, PictureUrl = "" };
        }),
        Total = Total(order),
    };

    private decimal Total(WorldOrder order) => order.Items.Sum(i => i.Units * world.Product(i.ProductId).Price);

    private static HttpResponseMessage Ok(object body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: JsonSerializerOptions.Web) };
}

/// <summary>Sends the signed-in Buyer's user name as the bearer token the <see cref="OrderingStub"/> answers by.</summary>
public sealed class BuyerTokenHandler(string userName) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new("Bearer", userName);
        return base.SendAsync(request, cancellationToken);
    }
}

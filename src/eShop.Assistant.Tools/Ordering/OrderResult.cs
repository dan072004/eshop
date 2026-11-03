using System.Text.Json.Serialization;

namespace eShop.Assistant.Tools.Ordering;

/// <summary>
/// The result of <c>get_order</c>: <c>{ today, orderNumber, orderDate, status, items: [ ... ] }</c>.
/// <see cref="Today"/> is the current UTC date, so the model doesn't guess it.
/// </summary>
public sealed record OrderResult(DateOnly Today, int OrderNumber, DateTime OrderDate, string Status, IReadOnlyList<OrderItemResult> Items);

/// <summary>
/// One order line. <see cref="Type"/> and <see cref="Brand"/> come from Catalog.API by <see cref="ProductId"/>, so
/// the model can apply per-type and per-brand Policy rules. Both are written as <c>null</c> (not left out, which is
/// the AI serializer's default) when Catalog doesn't know the product.
/// </summary>
public sealed record OrderItemResult(
    int ProductId,
    string ProductName,
    int Units,
    decimal UnitPrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Brand);

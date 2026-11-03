namespace eShop.Assistant.Tools.Ordering;

/// <summary>
/// The result of <c>list_my_orders</c>: <c>{ today, orders: [ { orderNumber, orderDate, status, total } ] }</c>,
/// newest-first as Ordering.API returns them. <see cref="Today"/> is the current UTC date, so the model doesn't guess it.
/// </summary>
public sealed record MyOrdersResult(DateOnly Today, IReadOnlyList<MyOrder> Orders);

public sealed record MyOrder(int OrderNumber, DateTime OrderDate, string Status, decimal Total);

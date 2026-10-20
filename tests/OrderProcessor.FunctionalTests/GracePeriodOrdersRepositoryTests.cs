namespace eShop.OrderProcessor.FunctionalTests;

public sealed class GracePeriodOrdersRepositoryTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task ReturnsSubmittedOrderJustPastTheGracePeriod()
    {
        var orderId = await SeedOrderAsync(placedAgo: GracePeriod + TimeSpan.FromSeconds(5));

        var orderIds = await CreateRepository().GetConfirmedGracePeriodOrdersAsync(GracePeriod, TestContext.Current.CancellationToken);

        Assert.Contains(orderId, orderIds);
    }

    [Fact]
    public async Task DoesNotReturnSubmittedOrderJustInsideTheGracePeriod()
    {
        var orderId = await SeedOrderAsync(placedAgo: GracePeriod - TimeSpan.FromSeconds(5));

        var orderIds = await CreateRepository().GetConfirmedGracePeriodOrdersAsync(GracePeriod, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(orderId, orderIds);
    }

    [Fact]
    public async Task DoesNotReturnOldOrderThatIsNoLongerSubmitted()
    {
        var orderId = await SeedOrderAsync(
            placedAgo: TimeSpan.FromHours(1),
            moveOn: order => order.SetAwaitingValidationStatus());

        var orderIds = await CreateRepository().GetConfirmedGracePeriodOrdersAsync(GracePeriod, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(orderId, orderIds);
    }

    private GracePeriodOrdersRepository CreateRepository() =>
        new(fixture.DataSource, NullLogger<GracePeriodOrdersRepository>.Instance);

    private async Task<int> SeedOrderAsync(TimeSpan placedAgo, Action<Order>? moveOn = null)
    {
        var order = new Order(
            "user-id", "user-name",
            new Address("street", "city", "state", "country", "zip"),
            cardTypeId: 1, cardNumber: "4012888888881881", cardSecurityNumber: "123",
            cardHolderName: "card holder", cardExpiration: DateTime.UtcNow.AddYears(1));
        moveOn?.Invoke(order);

        await using var context = fixture.CreateContext();
        context.Orders.Add(order);
        context.Entry(order).Property(o => o.OrderDate).CurrentValue = DateTime.UtcNow - placedAgo;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return order.Id;
    }
}

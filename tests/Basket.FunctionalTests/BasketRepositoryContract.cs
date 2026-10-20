namespace eShop.Basket.FunctionalTests;

// Behaviour every IBasketRepository must have. Each adapter (and each fake) gets a
// concrete subclass, so a fake can't drift from the real adapter.
public abstract class BasketRepositoryContract
{
    protected abstract IBasketRepository CreateRepository();

    [Fact]
    public async Task UpdateGetAndDeleteRoundTrip()
    {
        var repository = CreateRepository();
        var buyerId = $"buyer-{Guid.NewGuid():N}";
        var basket = new CustomerBasket
        {
            BuyerId = buyerId,
            Items =
            [
                new BasketItem
                {
                    ProductId = 42,
                    Quantity = 3,
                    ProductName = "Test product",
                    UnitPrice = 12.50m
                }
            ]
        };

        var updated = await repository.UpdateBasketAsync(basket);
        var loaded = await repository.GetBasketAsync(buyerId);
        var deleted = await repository.DeleteBasketAsync(buyerId);
        var afterDelete = await repository.GetBasketAsync(buyerId);

        Assert.NotNull(updated);
        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.Items.Single().ProductId);
        Assert.Equal(3, loaded.Items.Single().Quantity);
        Assert.True(deleted);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task MissingBasketReturnsNullAndDeleteReturnsFalse()
    {
        var repository = CreateRepository();
        var buyerId = $"missing-{Guid.NewGuid():N}";

        Assert.Null(await repository.GetBasketAsync(buyerId));
        Assert.False(await repository.DeleteBasketAsync(buyerId));
    }

    [Fact]
    public async Task StoredBasketIsASnapshotNotTheCallersObject()
    {
        var repository = CreateRepository();
        var buyerId = $"snapshot-{Guid.NewGuid():N}";
        var basket = new CustomerBasket
        {
            BuyerId = buyerId,
            Items = [new BasketItem { ProductId = 1, Quantity = 2, UnitPrice = 5m }]
        };

        var updated = await repository.UpdateBasketAsync(basket);
        basket.Items[0].Quantity = 99;
        basket.Items.Add(new BasketItem { ProductId = 2, Quantity = 1, UnitPrice = 1m });
        var loaded = await repository.GetBasketAsync(buyerId);
        loaded.Items[0].Quantity = 50;
        var reloaded = await repository.GetBasketAsync(buyerId);

        Assert.Equal(2, updated.Items.Single().Quantity);
        Assert.Equal(2, reloaded.Items.Single().Quantity);
    }

    [Fact]
    public async Task UpdateReplacesTheBuyersBasketAndLeavesOtherBuyersAlone()
    {
        var repository = CreateRepository();
        var buyerId = $"replace-{Guid.NewGuid():N}";
        var otherBuyerId = $"other-{Guid.NewGuid():N}";
        await repository.UpdateBasketAsync(new CustomerBasket
        {
            BuyerId = otherBuyerId,
            Items = [new BasketItem { ProductId = 7, Quantity = 1, UnitPrice = 3m }]
        });
        await repository.UpdateBasketAsync(new CustomerBasket
        {
            BuyerId = buyerId,
            Items = [new BasketItem { ProductId = 1, Quantity = 2, UnitPrice = 5m }]
        });

        await repository.UpdateBasketAsync(new CustomerBasket
        {
            BuyerId = buyerId,
            Items = [new BasketItem { ProductId = 2, Quantity = 4, UnitPrice = 8m }]
        });
        var loaded = await repository.GetBasketAsync(buyerId);
        var other = await repository.GetBasketAsync(otherBuyerId);

        Assert.Equal(2, loaded.Items.Single().ProductId);
        Assert.Equal(4, loaded.Items.Single().Quantity);
        Assert.Equal(7, other.Items.Single().ProductId);
    }
}

namespace eShop.Basket.FunctionalTests;

public sealed class RedisBasketRepositoryTests(RedisFixture fixture)
    : BasketRepositoryContract, IClassFixture<RedisFixture>
{
    protected override IBasketRepository CreateRepository() =>
        new RedisBasketRepository(NullLogger<RedisBasketRepository>.Instance, fixture.Connection);
}

namespace eShop.Basket.FunctionalTests;

public sealed class InMemoryBasketRepositoryTests : BasketRepositoryContract
{
    protected override IBasketRepository CreateRepository() => new InMemoryBasketRepository();
}

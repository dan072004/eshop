using System.Text.Json;

namespace eShop.Basket.FunctionalTests;

// Dictionary-backed fake of IBasketRepository. BasketRepositoryContract proves it
// behaves like RedisBasketRepository. Like Redis, it stores serialized snapshots,
// so callers never share an object with the store.
public sealed class InMemoryBasketRepository : IBasketRepository
{
    private readonly Dictionary<string, byte[]> _baskets = [];

    public Task<CustomerBasket> GetBasketAsync(string customerId) =>
        Task.FromResult(_baskets.TryGetValue(customerId, out var json)
            ? JsonSerializer.Deserialize(json, BasketSerializationContext.Default.CustomerBasket)!
            : null!);

    public Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket)
    {
        _baskets[basket.BuyerId] =
            JsonSerializer.SerializeToUtf8Bytes(basket, BasketSerializationContext.Default.CustomerBasket);
        return GetBasketAsync(basket.BuyerId);
    }

    public Task<bool> DeleteBasketAsync(string id) => Task.FromResult(_baskets.Remove(id));
}

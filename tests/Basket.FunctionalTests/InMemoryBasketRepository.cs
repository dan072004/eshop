using System.Text.Json;
using eShop.Basket.API.Model;
using eShop.Basket.API.Repositories;

namespace eShop.Basket.FunctionalTests;

// Dictionary-backed fake of IBasketRepository. BasketRepositoryContract proves it
// behaves like RedisBasketRepository. Like Redis, it stores serialized snapshots,
// so callers never share an object with the store.
// Basket.UnitTests links this file, so it relies on explicit usings, not either project's global usings.
public sealed class InMemoryBasketRepository : IBasketRepository
{
    private readonly Dictionary<string, byte[]> _baskets = [];

    // When set, saves are not stored and UpdateBasketAsync returns null, as Redis does when the write fails.
    public bool RefusesSaves { get; set; }

    public Task<CustomerBasket> GetBasketAsync(string customerId) =>
        Task.FromResult(_baskets.TryGetValue(customerId, out var json)
            ? JsonSerializer.Deserialize(json, BasketSerializationContext.Default.CustomerBasket)!
            : null!);

    public Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket)
    {
        if (RefusesSaves)
        {
            return Task.FromResult<CustomerBasket>(null!);
        }

        _baskets[basket.BuyerId] =
            JsonSerializer.SerializeToUtf8Bytes(basket, BasketSerializationContext.Default.CustomerBasket);
        return GetBasketAsync(basket.BuyerId);
    }

    public Task<bool> DeleteBasketAsync(string id) => Task.FromResult(_baskets.Remove(id));
}

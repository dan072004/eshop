using System.Security.Claims;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// The fixed world every eval case is written against, loaded from <c>World/world.json</c>: the Buyers, their orders,
/// the catalog products in those orders, and the moment the clock is stopped at. Cases refer to orders by number.
/// </summary>
public sealed record EvalWorld(
    DateTimeOffset Now,
    IReadOnlyList<WorldBuyer> Buyers,
    IReadOnlyList<WorldOrder> Orders,
    IReadOnlyList<WorldProduct> Products)
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "World", "world.json");

    public static EvalWorld Load() =>
        JsonSerializer.Deserialize<EvalWorld>(File.ReadAllText(FilePath), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException($"{FilePath} is empty.");

    public WorldBuyer Buyer(string userName) =>
        Buyers.SingleOrDefault(b => b.UserName == userName)
            ?? throw new ArgumentException($"The world has no Buyer '{userName}'.", nameof(userName));

    public WorldProduct Product(int id) =>
        Products.SingleOrDefault(p => p.Id == id)
            ?? throw new InvalidDataException($"The world has no product {id}.");
}

/// <summary>A Buyer and the claims Identity issues for them (the claim types WebApp's <c>GetUserInfo</c> reads).</summary>
public sealed record WorldBuyer(string UserName, IReadOnlyDictionary<string, string> Claims)
{
    public ClaimsPrincipal ToPrincipal() =>
        new(new ClaimsIdentity(
            [new Claim("sub", UserName), .. Claims.Select(c => new Claim(c.Key, c.Value))],
            authenticationType: "eval"));
}

public sealed record WorldOrder(int OrderNumber, string Buyer, DateTime OrderDate, string Status, IReadOnlyList<WorldOrderItem> Items);

public sealed record WorldOrderItem(int ProductId, int Units);

/// <summary>A catalog product as Catalog's seed has it: its type and brand are names, as in <c>catalog.json</c>.</summary>
public sealed record WorldProduct(int Id, string Name, string Description, decimal Price, string Type, string Brand);

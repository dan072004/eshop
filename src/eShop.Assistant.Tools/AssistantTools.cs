using System.ComponentModel;
using eShop.Assistant.Tools.Catalog;
using eShop.Assistant.Tools.Ordering;
using eShop.Assistant.Tools.Policy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace eShop.Assistant.Tools;

/// <summary>
/// The shared assistant tools as <see cref="AIFunction"/>s with contract names and descriptions, ready for a host
/// to pass into <see cref="ChatOptions.Tools"/> or wrap as MCP tools. Resolve it from DI after
/// <see cref="AssistantToolsServiceCollectionExtensions.AddAssistantTools"/>.
/// </summary>
public sealed partial class AssistantTools
{
    private const int CatalogSearchPageSize = 8;
    private const int PolicySearchTop = 5;
    private const string CatalogUnavailableMessage = "Error accessing catalog.";

    /// <summary>
    /// The citation rule travels with the tool, so any model that calls it (including MCP clients, which never see
    /// WebApp's system prompt) is told to state rules only from results and to cite each one.
    /// </summary>
    public const string SearchPoliciesDescription =
        "AdventureWorks Policies on returns, exchanges, shipping, warranty, sizing, payments and orders. " +
        "State Policy rules only from these results; cite each rule with its citation value; " +
        "if no result answers the question, say so.";

    /// <summary>
    /// Points the model at <c>get_order</c>: without it, a model that only lists orders never sees the item types
    /// that decide the Return Window, and answers with the standard one.
    /// </summary>
    public const string ListMyOrdersDescription =
        "Lists the signed-in Buyer's own AdventureWorks orders, newest first, with today's date (UTC). " +
        "Each order has its order number, Order Date, status and total, but no items; " +
        "call get_order for an order's items.";

    /// <summary>
    /// Tells the model to search with the item types, so a search can find the Policy Sections for those types
    /// (e.g. the Navigation Return Window) and not only the standard rule.
    /// </summary>
    public const string GetOrderDescription =
        "Gets one of the signed-in Buyer's own AdventureWorks orders by its order number, with today's date (UTC). " +
        "It has the Order Date, status and items; each item has its product id and name, units, unit price, " +
        "and its catalog type and brand (null when the catalog doesn't know the product). " +
        "Some Policy rules, such as Return Windows, depend on an item's type: name the types when searching the Policies.";

    private readonly CatalogClient _catalog;
    private readonly PolicyClient _policy;
    private readonly OrderingClient _ordering;
    private readonly TimeProvider _timeProvider;
    private readonly ProductPictureUrl? _pictureUrl;
    private readonly ILogger _logger;

    public AssistantTools(
        CatalogClient catalog,
        PolicyClient policy,
        OrderingClient ordering,
        TimeProvider timeProvider,
        ILogger<AssistantTools> logger,
        ProductPictureUrl? pictureUrl = null)
    {
        _catalog = catalog;
        _policy = policy;
        _ordering = ordering;
        _timeProvider = timeProvider;
        _pictureUrl = pictureUrl;
        _logger = logger;

        SearchPolicies = AIFunctionFactory.Create(SearchPoliciesAsync, new AIFunctionFactoryOptions
        {
            Name = AssistantToolNames.SearchPolicies,
            Description = SearchPoliciesDescription,
        });

        SearchCatalog = AIFunctionFactory.Create(SearchCatalogAsync, new AIFunctionFactoryOptions
        {
            Name = AssistantToolNames.SearchCatalog,
            Description = "Searches the AdventureWorks catalog for a provided product description",
        });

        ListMyOrders = AIFunctionFactory.Create(ListMyOrdersAsync, new AIFunctionFactoryOptions
        {
            Name = AssistantToolNames.ListMyOrders,
            Description = ListMyOrdersDescription,
        });

        GetOrder = AIFunctionFactory.Create(GetOrderAsync, new AIFunctionFactoryOptions
        {
            Name = AssistantToolNames.GetOrder,
            Description = GetOrderDescription,
        });

        All = [SearchPolicies, ListMyOrders, GetOrder, SearchCatalog];
    }

    /// <summary><c>search_policies(question)</c>.</summary>
    public AIFunction SearchPolicies { get; }

    /// <summary><c>list_my_orders()</c>.</summary>
    public AIFunction ListMyOrders { get; }

    /// <summary><c>get_order(orderNumber)</c>.</summary>
    public AIFunction GetOrder { get; }

    /// <summary><c>search_catalog(productDescription)</c>.</summary>
    public AIFunction SearchCatalog { get; }

    /// <summary>Every tool in the library, in contract order.</summary>
    public IReadOnlyList<AIFunction> All { get; }

    private Task<object> SearchPoliciesAsync(
        [Description("The Buyer's question about an AdventureWorks Policy")] string question,
        CancellationToken cancellationToken) =>
        GuardAsync(() => _policy.SearchAsync(question, PolicySearchTop, cancellationToken),
            "Error searching the Policies.", cancellationToken);

    private Task<object> SearchCatalogAsync(
        [Description("The product description for which to search")] string productDescription,
        CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var page = await _catalog.SearchBySemanticRelevanceAsync(productDescription, 0, CatalogSearchPageSize, cancellationToken);
            return page with
            {
                Data = [.. page.Data.Select(item => item with { PictureUrl = _pictureUrl?.Invoke(item.Id) })]
            };
        }, CatalogUnavailableMessage, cancellationToken);

    /// <summary>
    /// The one error guard every tool call goes through: returns the call's result, or the <see cref="ToolError"/>
    /// the model sees instead. <paramref name="knownError"/> maps the failures a backend gives a meaning to (e.g.
    /// Ordering's 401 and 404); any other exception is logged and becomes <c>unavailable</c>. Only the caller's
    /// own cancellation is rethrown.
    /// </summary>
    private async Task<object> GuardAsync<T>(
        Func<Task<T>> call,
        string unavailableMessage,
        CancellationToken cancellationToken,
        Func<Exception, ToolError?>? knownError = null)
        where T : notnull
    {
        try
        {
            return await call();
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (knownError?.Invoke(e) is { } error)
            {
                return error;
            }

            _logger.LogError(e, "Tool call failed: {ToolErrorMessage}", unavailableMessage);
            return ToolError.Unavailable(unavailableMessage);
        }
    }
}

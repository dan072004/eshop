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

    /// <summary>
    /// The citation rule travels with the tool, so any model that calls it (including MCP clients, which never see
    /// WebApp's system prompt) is told to state rules only from results and to cite each one.
    /// </summary>
    public const string SearchPoliciesDescription =
        "AdventureWorks Policies on returns, exchanges, shipping, warranty, sizing, payments and orders. " +
        "State Policy rules only from these results; cite each rule with its citation value; " +
        "if no result answers the question, say so.";

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
            Description = "Lists the signed-in Buyer's own AdventureWorks orders, newest first, with today's date (UTC). "
                + "Each order has its order number, Order Date, status and total.",
        });

        All = [SearchPolicies, ListMyOrders, SearchCatalog];
    }

    /// <summary><c>search_policies(question)</c>.</summary>
    public AIFunction SearchPolicies { get; }

    /// <summary><c>list_my_orders()</c>.</summary>
    public AIFunction ListMyOrders { get; }

    /// <summary><c>search_catalog(productDescription)</c>.</summary>
    public AIFunction SearchCatalog { get; }

    /// <summary>Every tool in the library, in contract order.</summary>
    public IReadOnlyList<AIFunction> All { get; }

    private async Task<object> SearchPoliciesAsync(
        [Description("The Buyer's question about an AdventureWorks Policy")] string question,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _policy.SearchAsync(question, PolicySearchTop, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            const string Message = "Error searching the Policies.";
            _logger.LogError(e, Message);
            return ToolError.Unavailable(Message);
        }
    }

    private async Task<object> SearchCatalogAsync(
        [Description("The product description for which to search")] string productDescription,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _catalog.SearchBySemanticRelevanceAsync(productDescription, 0, CatalogSearchPageSize, cancellationToken);
            return page with
            {
                Data = [.. page.Data.Select(item => item with { PictureUrl = _pictureUrl?.Invoke(item.Id) })]
            };
        }
        catch (HttpRequestException e)
        {
            const string Message = "Error accessing catalog.";
            _logger.LogError(e, Message);
            return ToolError.Unavailable(Message);
        }
    }
}

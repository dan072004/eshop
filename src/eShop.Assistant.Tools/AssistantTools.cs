using System.ComponentModel;
using eShop.Assistant.Tools.Catalog;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace eShop.Assistant.Tools;

/// <summary>
/// The shared assistant tools as <see cref="AIFunction"/>s with contract names and descriptions, ready for a host
/// to pass into <see cref="ChatOptions.Tools"/> or wrap as MCP tools. Resolve it from DI after
/// <see cref="AssistantToolsServiceCollectionExtensions.AddAssistantTools"/>.
/// </summary>
public sealed class AssistantTools
{
    private const int CatalogSearchPageSize = 8;

    private readonly CatalogClient _catalog;
    private readonly ProductPictureUrl? _pictureUrl;
    private readonly ILogger _logger;

    public AssistantTools(CatalogClient catalog, ILogger<AssistantTools> logger, ProductPictureUrl? pictureUrl = null)
    {
        _catalog = catalog;
        _pictureUrl = pictureUrl;
        _logger = logger;

        SearchCatalog = AIFunctionFactory.Create(SearchCatalogAsync, new AIFunctionFactoryOptions
        {
            Name = AssistantToolNames.SearchCatalog,
            Description = "Searches the AdventureWorks catalog for a provided product description",
        });

        All = [SearchCatalog];
    }

    /// <summary><c>search_catalog(productDescription)</c>.</summary>
    public AIFunction SearchCatalog { get; }

    /// <summary>Every tool in the library, in contract order.</summary>
    public IReadOnlyList<AIFunction> All { get; }

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

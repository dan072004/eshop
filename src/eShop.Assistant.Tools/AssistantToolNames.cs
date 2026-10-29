namespace eShop.Assistant.Tools;

/// <summary>
/// The tool names in the assistant tool contract. Hosts (WebApp's Shopping Assistant, eShop.Mcp) and models
/// see these names, so change one only on purpose and update both specs when you do.
/// </summary>
public static class AssistantToolNames
{
    public const string ListMyOrders = "list_my_orders";
    public const string SearchCatalog = "search_catalog";
}

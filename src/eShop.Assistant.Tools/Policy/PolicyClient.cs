using System.Net.Http.Json;

namespace eShop.Assistant.Tools.Policy;

/// <summary>Typed client for policy-api, addressed by its Aspire service name. Policies are public, so it sends no token.</summary>
public sealed class PolicyClient(HttpClient httpClient)
{
    public const string BaseAddress = "https+http://policy-api";

    /// <exception cref="HttpRequestException">policy-api could not be reached or returned an error status.</exception>
    public async Task<PolicySearchResult> SearchAsync(string question, int top, CancellationToken cancellationToken)
    {
        var uri = $"api/policies/search?q={Uri.EscapeDataString(question)}&top={top}";
        var result = await httpClient.GetFromJsonAsync<PolicySearchResult>(uri, cancellationToken);
        return result ?? throw new HttpRequestException("policy-api returned an empty body.");
    }
}

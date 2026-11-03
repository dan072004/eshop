using System.Net.Http.Json;
using System.Text.Json;

namespace eShop.Policy.FunctionalTests;

/// <summary>Calls <c>GET /api/policies/search</c> the way WebApp's PolicyClient does, and reads the response.</summary>
internal static class PolicySearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PolicySearchResponse> SearchPoliciesAsync(this HttpClient client, string question, int? top = null)
    {
        var url = $"/api/policies/search?q={Uri.EscapeDataString(question)}" + (top is null ? "" : $"&top={top}");
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PolicySearchResponse>(JsonOptions, TestContext.Current.CancellationToken);
    }
}

internal sealed record PolicySearchResponse(string Mode, List<PolicySearchResult> Results);

internal sealed record PolicySearchResult(
    string Citation, string PolicySlug, string PolicyTitle, string Section, string Anchor, string Text, double Score);

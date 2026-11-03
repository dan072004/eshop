using eShop.Policy.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace eShop.Policy.API.Apis;

public static class PolicyApi
{
    public const int DefaultTop = 5;

    public static IEndpointRouteBuilder MapPolicyApi(this IEndpointRouteBuilder app)
    {
        // Policies are public, so search is anonymous and read-only.
        app.MapGet("/api/policies/search", SearchPolicies)
            .WithName("SearchPolicies")
            .WithSummary("Search Policy Sections")
            .WithDescription($"Returns the Policy Sections that best answer the question, each with a ready-made Citation. top defaults to {DefaultTop} and is capped at {PolicySearch.MaxTop}.")
            .AllowAnonymous();

        return app;
    }

    public static async Task<Results<Ok<PolicySearchResponse>, ValidationProblem>> SearchPolicies(
        string? q,
        int? top,
        PolicySearch search,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(q))
        {
            errors["q"] = ["The question is required."];
        }

        if (top < 1)
        {
            errors["top"] = ["top must be at least 1."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var response = await search.SearchAsync(q!, top ?? DefaultTop, cancellationToken);

        loggerFactory.CreateLogger(typeof(PolicyApi))
            .LogInformation("Policy search in {Mode} mode returned {ResultCount} sections", response.Mode, response.Results.Count);

        return TypedResults.Ok(response);
    }
}

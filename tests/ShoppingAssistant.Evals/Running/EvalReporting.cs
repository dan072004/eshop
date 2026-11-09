using System.Reflection;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// Where results go and which checks score them. Every check is an evaluator in the Reporting framework, so all
/// verdicts land in one disk store that <c>dotnet aieval report</c> renders.
/// </summary>
public static class EvalReporting
{
    public const string StoreVariable = "ESHOP_EVALS_STORE";

    /// <summary><c>ESHOP_EVALS_STORE</c>, or <c>evals-store</c> under the build's artifacts directory.</summary>
    public static string StorePath =>
        Environment.GetEnvironmentVariable(StoreVariable) is { Length: > 0 } path
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(ArtifactsPath, "evals-store"));

    public static IReadOnlyList<IEvaluator> DeterministicChecks(EvalWorld world) =>
    [
        new ToolsEvaluator(),
        new CitationsPresentEvaluator(),
        new CitationsGroundedEvaluator(),
        new StringsEvaluator(),
        new OtherBuyersOrdersEvaluator(world),
    ];

    /// <summary>One execution, named by its UTC start time, so the report can show runs side by side.</summary>
    public static ReportingConfiguration Create(EvalWorld world) =>
        DiskBasedReportingConfiguration.Create(
            StorePath,
            DeterministicChecks(world),
            enableResponseCaching: false,
            executionName: DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture));

    // Set by the project file from MSBuild's ArtifactsPath, so --artifacts-path moves the store too.
    private static string ArtifactsPath =>
        typeof(EvalReporting).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "ArtifactsPath")?.Value is { Length: > 0 } path
            ? path
            : throw new InvalidOperationException($"The build recorded no ArtifactsPath; set {StoreVariable}.");
}

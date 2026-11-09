using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Judge;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// Where results go and which checks score them. Every check is an evaluator in the Reporting framework, so all
/// verdicts land in one disk store that <c>dotnet aieval report</c> renders.
/// </summary>
public static class EvalReporting
{
    public const string StoreVariable = "ESHOP_EVALS_STORE";

    /// <summary>
    /// This test run's execution name, its UTC start time, shared by the cases and the calibration set so the report
    /// shows them as one run and shows runs side by side.
    /// </summary>
    public static string ExecutionName { get; } = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

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

    /// <summary>The cache both the chat model's turns and the judge's calls go through, in the results store.</summary>
    public static IEvaluationResponseCacheProvider ResponseCacheProvider => ResponseCache.Provider(StorePath, ResponseCache.IsFresh);

    /// <summary>
    /// The cases: every deterministic check, then the judge's rubric verdicts. Each case's scenario run takes its
    /// <see cref="CaseTranscript.CachingKeys"/> and <see cref="CaseTranscript.OriginTag"/>.
    /// </summary>
    public static ReportingConfiguration Create(EvalWorld world, ChatConfiguration judge) =>
        Create([.. DeterministicChecks(world), new RubricEvaluator()], judge);

    /// <summary>The calibration set: the judge's rubric verdicts only, in scenarios named <c>judge-calibration.{id}</c>.</summary>
    public static ReportingConfiguration CreateCalibration(ChatConfiguration judge) => Create([new RubricEvaluator()], judge);

    // The judge's calls are cached too, keyed also by the judge settings Reporting can't see.
    private static ReportingConfiguration Create(IEnumerable<IEvaluator> evaluators, ChatConfiguration judge) =>
        new(evaluators, new DiskBasedResultStore(StorePath), judge, ResponseCacheProvider, CachingKeys.Judge, ExecutionName);

    // Set by the project file from MSBuild's ArtifactsPath, so --artifacts-path moves the store too.
    private static string ArtifactsPath =>
        typeof(EvalReporting).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "ArtifactsPath")?.Value is { Length: > 0 } path
            ? path
            : throw new InvalidOperationException($"The build recorded no ArtifactsPath; set {StoreVariable}.");
}

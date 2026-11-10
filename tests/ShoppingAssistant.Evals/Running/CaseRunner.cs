using System.Security.Claims;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// Runs one case through the Shopping Assistant's conversation class, built as WebApp builds it: WebApp's Claude chat
/// pipeline around <paramref name="model"/>'s cached client, the tools library over the real policy-api and the fixed
/// world's stubs, and the case's Buyer. It sends the case's turns and returns every message: answers, tool calls and
/// tool results.
/// </summary>
public sealed class CaseRunner(EvalWorld world, PolicyApiFixture policyApi, CachedModel model, IConfiguration configuration)
{
    /// <exception cref="ModelCallFailedException">The model call failed, which is an infrastructure error, not an answer.</exception>
    public async Task<CaseTranscript> RunAsync(
        EvalCase evalCase, string iterationName = Defaults.DefaultIterationName, CancellationToken cancellationToken = default)
    {
        var errors = new ModelErrors();
        var services = new ServiceCollection().AddLogging(logging => logging.AddProvider(errors));
        var tools = services.AddEvalWorld(world, evalCase.SignedInAs, evalCase.CatalogResults);
        tools.PolicyClient
            .ConfigureHttpClient(client => client.BaseAddress = policyApi.Server.BaseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => policyApi.Server.CreateHandler());

        await using var provider = services.BuildServiceProvider();
        var basket = new OutOfScopeBasket();
        ChatState Conversation(IChatClient chatClient) => new(
            basket,
            basket,
            evalCase.SignedInAs is { } userName ? world.Buyer(userName).ToPrincipal() : new ClaimsPrincipal(new ClaimsIdentity()),
            provider.GetRequiredService<eShop.Assistant.Tools.AssistantTools>(),
            provider.GetRequiredService<ILoggerFactory>(),
            chatClient);

        var cachingKeys = CachingKeys.ForCase(
            await ToolsProbe.ToolsOfAsync(Conversation),
            await File.ReadAllTextAsync(EvalCase.FilePath, cancellationToken),
            evalCase.Id,
            await File.ReadAllTextAsync(EvalWorld.FilePath, cancellationToken));
        var sample = await model.OpenAsync(evalCase.Id, iterationName, cachingKeys, cancellationToken);
        var chat = Conversation(ClaudeChatPipeline.Build(sample.ChatClient, configuration, provider));

        foreach (var turn in evalCase.Turns)
        {
            await chat.AddUserMessageAsync(turn, () => { });

            // The conversation class turns a failed model call into an apology; that is not the model's answer.
            if (errors.First is { } error)
            {
                throw new ModelCallFailedException(evalCase.Id, error);
            }
        }

        return new CaseTranscript(chat.Messages.ToList()) { CachingKeys = cachingKeys, ModelCalls = sample.ModelCalls, Usage = sample.Usage };
    }

    // The tools the conversation class sends, read off its first request; it asks no model.
    private sealed class ToolsProbe : IChatClient
    {
        private IList<AITool> _tools = [];

        public static async Task<IList<AITool>> ToolsOfAsync(Func<IChatClient, ChatState> conversation)
        {
            using var probe = new ToolsProbe();
            await conversation(probe).AddUserMessageAsync("", () => { });
            return probe._tools;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _tools = options?.Tools ?? [];
            return Task.FromResult(new ChatResponse());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    // Records the exception the conversation class logs when a model call fails.
    private sealed class ModelErrors : ILoggerProvider
    {
        public Exception? First { get; private set; }

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(ChatState).FullName ? new Logger(this) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose() { }

        private sealed class Logger(ModelErrors errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error && exception is not null && formatter(state, exception) == "Error getting chat completions.")
                {
                    errors.First ??= exception;
                }
            }
        }
    }
}

/// <summary>A finished conversation, split as the evaluators read it: up to the last user turn, then the reply to it.</summary>
public sealed record CaseTranscript(IReadOnlyList<ChatMessage> Messages)
{
    /// <summary>The keys the chat turns were cached under; the judge's call is keyed by them too.</summary>
    public IReadOnlyList<string> CachingKeys { get; init; } = [];

    /// <summary>How many chat turns reached the model; 0 means the whole answer was replayed from the cache.</summary>
    public int ModelCalls { get; init; }

    /// <summary>Cached when no chat turn reached the model: the answer is reused evidence, not a new sample.</summary>
    public Gate.SampleOrigin Origin => ModelCalls == 0 ? Gate.SampleOrigin.Cached : Gate.SampleOrigin.Fresh;

    /// <summary>The origin as a tag on the stored result (<c>sample:cached</c> or <c>sample:fresh</c>), so the report shows it.</summary>
    public string OriginTag => "sample:" + Origin.ToString().ToLowerInvariant();

    private int LastUserTurn => Messages.ToList().FindLastIndex(m => m.Role == ChatRole.User);

    public IReadOnlyList<ChatMessage> Prompt => [.. Messages.Take(LastUserTurn + 1)];

    public ChatResponse Reply => new([.. Messages.Skip(LastUserTurn + 1)]);

    /// <summary>The tokens each chat turn that reached the model reported; a replayed turn cost nothing.</summary>
    public IReadOnlyList<Gate.TokenUsage> Usage { get; init; } = [];
}

public sealed class ModelCallFailedException(string caseId, Exception inner)
    : Exception($"Case '{caseId}': the model call failed, so the run has no answer to score: {inner.Message}", inner);

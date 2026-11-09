using System.Security.Claims;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// Runs one case through the Shopping Assistant's conversation class, built as WebApp builds it: WebApp's Claude chat
/// pipeline around <paramref name="model"/>, the tools library over the real policy-api and the fixed world's stubs,
/// and the case's Buyer. It sends the case's turns and returns every message: answers, tool calls and tool results.
/// </summary>
public sealed class CaseRunner(EvalWorld world, PolicyApiFixture policyApi, IChatClient model, IConfiguration configuration)
{
    /// <exception cref="ModelCallFailedException">The model call failed, which is an infrastructure error, not an answer.</exception>
    public async Task<CaseTranscript> RunAsync(EvalCase evalCase)
    {
        var errors = new ModelErrors();
        var services = new ServiceCollection().AddLogging(logging => logging.AddProvider(errors));
        var tools = services.AddEvalWorld(world, evalCase.SignedInAs, evalCase.CatalogResults);
        tools.PolicyClient
            .ConfigureHttpClient(client => client.BaseAddress = policyApi.Server.BaseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => policyApi.Server.CreateHandler());

        await using var provider = services.BuildServiceProvider();
        var usage = new UsageRecorder(model);
        var basket = new OutOfScopeBasket();
        var chat = new ChatState(
            basket,
            basket,
            evalCase.SignedInAs is { } userName ? world.Buyer(userName).ToPrincipal() : new ClaimsPrincipal(new ClaimsIdentity()),
            provider.GetRequiredService<eShop.Assistant.Tools.AssistantTools>(),
            provider.GetRequiredService<ILoggerFactory>(),
            ClaudeChatPipeline.Build(usage, configuration, provider));

        foreach (var turn in evalCase.Turns)
        {
            await chat.AddUserMessageAsync(turn, () => { });

            // The conversation class turns a failed model call into an apology; that is not the model's answer.
            if (errors.First is { } error)
            {
                throw new ModelCallFailedException(evalCase.Id, error);
            }
        }

        return new CaseTranscript(chat.Messages.ToList()) { Usage = usage.Turns };
    }

    // Records the tokens each model turn reports; it sits below function invocation, so it sees every turn.
    private sealed class UsageRecorder(IChatClient inner) : DelegatingChatClient(inner)
    {
        private readonly List<Gate.TokenUsage> _turns = [];

        public IReadOnlyList<Gate.TokenUsage> Turns => _turns;

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            if (response.Usage is { } usage)
            {
                _turns.Add(new(response.ModelId ?? options?.ModelId ?? "unknown", usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0));
            }
            return response;
        }
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
    private int LastUserTurn => Messages.ToList().FindLastIndex(m => m.Role == ChatRole.User);

    public IReadOnlyList<ChatMessage> Prompt => [.. Messages.Take(LastUserTurn + 1)];

    public ChatResponse Reply => new([.. Messages.Skip(LastUserTurn + 1)]);

    /// <summary>The tokens each model turn reported.</summary>
    public IReadOnlyList<Gate.TokenUsage> Usage { get; init; } = [];
}

public sealed class ModelCallFailedException(string caseId, Exception inner)
    : Exception($"Case '{caseId}': the model call failed, so the run has no answer to score: {inner.Message}", inner);

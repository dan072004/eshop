using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

public static class ChatTelemetryExtensions
{
    /// <summary>
    /// Adds the OpenTelemetry step that every Chat Provider shares. Placed inside function invocation, it makes each model
    /// call a <c>chat</c> span under the Turn's <c>orchestrate_tools</c> span. Spans carry message content only in
    /// Development, read from the host environment the pipeline is built with; without one, they carry none.
    /// </summary>
    public static ChatClientBuilder UseChatTelemetry(this ChatClientBuilder builder)
        => builder.Use((inner, services) => new OpenTelemetryChatClient(
            inner, services.GetService<ILoggerFactory>()?.CreateLogger(typeof(OpenTelemetryChatClient)))
        {
            EnableSensitiveData = services.GetService<IHostEnvironment>()?.IsDevelopment() == true,
        });
}

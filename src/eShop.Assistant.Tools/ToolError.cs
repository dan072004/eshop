namespace eShop.Assistant.Tools;

/// <summary>
/// A failure returned to the model as tool output (never thrown), shaped <c>{ error, message }</c>.
/// </summary>
public sealed record ToolError(string Error, string Message)
{
    /// <summary>Any failure that isn't covered by a more specific error. The tool logs it.</summary>
    public static ToolError Unavailable(string message) => new("unavailable", message);
}

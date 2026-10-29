namespace eShop.Assistant.Tools;

/// <summary>
/// A failure returned to the model as tool output (never thrown), shaped <c>{ error, message }</c>.
/// </summary>
public sealed record ToolError(string Error, string Message)
{
    /// <summary>Any failure that isn't covered by a more specific error. The tool logs it.</summary>
    public static ToolError Unavailable(string message) => new("unavailable", message);

    /// <summary>The backend needs the Buyer to sign in: no token was available, or it returned 401.</summary>
    public static ToolError SignInRequired(string message) => new("sign_in_required", message);

    /// <summary>
    /// The backend returned 404. For orders this covers both unknown orders and other Buyers' orders, which
    /// Ordering.API deliberately doesn't tell apart.
    /// </summary>
    public static ToolError NotFound(string message) => new("not_found", message);
}

namespace eShop.WebApp.Services.AccessTokens;

/// <summary>
/// The Buyer's access token for the interactive session (circuit), where <c>HttpContext</c> isn't reliably available.
/// It is read while the page is still rendered on the server (see <c>ShowChatbotButton</c>), passed to the interactive
/// <c>Chatbot</c> as a parameter, and stored here in the circuit's scope. Empty when the Buyer isn't signed in.
/// </summary>
public sealed class AccessTokenHolder
{
    public string? AccessToken { get; set; }
}

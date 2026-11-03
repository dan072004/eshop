namespace eShop.Assistant.Tools;

/// <summary>
/// Turns a product id into a picture URL the host's front end can show. Supplied by the host through
/// <see cref="AssistantToolsBuilder.WithProductPictureUrl"/>; without one, catalog results carry no <c>pictureUrl</c>.
/// </summary>
public delegate string ProductPictureUrl(int productId);

namespace eShop.Policy.API;

/// <summary>Bound from the <c>Policies</c> configuration section.</summary>
public class PolicyOptions
{
    public const string SectionName = "Policies";

    /// <summary>
    /// The folder of Policy files to index (<c>Policies:Folder</c>). Defaults to the copy of the repo's
    /// <c>policies/</c> folder that ships in the build output.
    /// </summary>
    public string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "policies");
}

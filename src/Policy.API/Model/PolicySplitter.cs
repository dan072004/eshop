using System.Text;

namespace eShop.Policy.API.Model;

/// <summary>Turns a Policy file (YAML front matter plus Markdown) into a <see cref="ParsedPolicy"/>.</summary>
/// <remarks>
/// Each <c>##</c> heading starts a Policy Section; <c>###</c> headings and tables stay inside it.
/// Text between the <c>#</c> title and the first <c>##</c> becomes an intro section headed by the Policy title.
/// </remarks>
public static class PolicySplitter
{
    public static ParsedPolicy Split(string fileName, string content)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var (frontMatter, bodyStart) = ReadFrontMatter(lines);

        var title = frontMatter.GetValueOrDefault("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException($"Policy file '{fileName}' has no title in its front matter.");
        }

        return new ParsedPolicy(
            Path.GetFileNameWithoutExtension(fileName),
            title,
            frontMatter.GetValueOrDefault("category") ?? "",
            frontMatter.GetValueOrDefault("last_updated"),
            SplitSections(title, lines.Skip(bodyStart)));
    }

    public static string Citation(string policyTitle, string heading) =>
        heading == policyTitle ? policyTitle : $"{policyTitle} › {heading}";

    private static List<PolicySection> SplitSections(string title, IEnumerable<string> lines)
    {
        var sections = new List<PolicySection>();
        var heading = title; // the intro, until the first "##"
        var body = new StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim('\n', ' ', '\t');
            // An empty intro is dropped; a "##" section is kept even if it has no body.
            if (text.Length > 0 || heading != title || sections.Count > 0)
            {
                sections.Add(new PolicySection(
                    sections.Count,
                    heading,
                    Anchor(heading),
                    text,
                    Citation(title, heading),
                    $"{title} › {heading}\n\n{text}"));
            }

            body.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[3..].Trim();
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                // The "#" title repeats the front matter title; it is not body text.
            }
            else
            {
                body.Append(line.TrimEnd()).Append('\n');
            }
        }

        Flush();
        return sections;
    }

    // A GitHub-style slug: lower case, letters and digits kept, every other run of characters becomes one hyphen.
    private static string Anchor(string heading)
    {
        var anchor = new StringBuilder();
        foreach (var c in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                anchor.Append(c);
            }
            else if ((c == ' ' || c == '-') && anchor.Length > 0 && anchor[^1] != '-')
            {
                anchor.Append('-');
            }
        }

        return anchor.ToString().TrimEnd('-');
    }

    // Front matter is flat "key: value" lines between two "---" lines; a value may itself contain colons.
    private static (Dictionary<string, string> Values, int BodyStart) ReadFrontMatter(string[] lines)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (values, 0);
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---")
            {
                return (values, i + 1);
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                values[line[..colon].Trim()] = line[(colon + 1)..].Trim().Trim('"', '\'');
            }
        }

        return (values, lines.Length);
    }
}

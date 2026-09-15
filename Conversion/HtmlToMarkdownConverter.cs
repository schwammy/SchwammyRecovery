using System.Net;
using System.Text;
using HtmlAgilityPack;
using SchwammyRecovery.Extraction;

namespace SchwammyRecovery.Conversion;

public interface IHtmlToMarkdownConverter
{
    string Convert(
        string html,
        IReadOnlyList<RecoveredImage> images,
        string imagePathPrefix,
        string? imagesDirectory = null);
}

public sealed class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    public string Convert(
        string html,
        IReadOnlyList<RecoveredImage> images,
        string imagePathPrefix,
        string? imagesDirectory = null)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var imagesBySourceUrl = images
            .Where(image => !string.IsNullOrWhiteSpace(image.SourceUrl))
            .GroupBy(
                image => image.SourceUrl,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder();

        foreach (var node in document.DocumentNode.ChildNodes)
        {
            ConvertNode(
                node,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                0);
        }

        return NormalizeMarkdown(builder.ToString());
    }

    private static void ConvertNode(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            AppendText(builder, node.InnerText);
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
            return;

        switch (node.Name.ToLowerInvariant())
        {
            case "p":
                if (LooksLikeCodeBlock(node))
                {
                    AppendCodeBlock(
                        node,
                        builder,
                        language: "csharp");
                    break;
                }

                ConvertChildren(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);

                AppendParagraphBreak(builder);
                break;

            case "br":
                builder.Append('\n');
                break;

            case "pre":
                AppendCodeBlock(
                    node,
                    builder,
                    GetCodeLanguage(node));
                break;

            case "code":
                builder.Append('`');
                AppendCodeText(node, builder);
                builder.Append('`');
                break;

            case "h1":
                AppendHeading(
                    node,
                    builder,
                    1,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "h2":
                AppendHeading(
                    node,
                    builder,
                    2,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "h3":
                AppendHeading(
                    node,
                    builder,
                    3,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "h4":
                AppendHeading(
                    node,
                    builder,
                    4,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "h5":
                AppendHeading(
                    node,
                    builder,
                    5,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "h6":
                AppendHeading(
                    node,
                    builder,
                    6,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "strong":
            case "b":
                AppendInlineWrapper(
                    node,
                    builder,
                    "**",
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "em":
            case "i":
                AppendInlineWrapper(
                    node,
                    builder,
                    "*",
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "a":
                AppendLink(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "img":
                AppendImage(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory);
                break;

            case "ul":
                ConvertList(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth,
                    ordered: false);
                break;

            case "ol":
                ConvertList(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth,
                    ordered: true);
                break;

            case "li":
                ConvertChildren(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "blockquote":
                AppendBlockquote(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            case "div":
                ConvertChildren(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);

                AppendParagraphBreak(builder);
                break;

            case "span":
            case "font":
            case "label":
            case "small":
                ConvertChildren(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;

            default:
                ConvertChildren(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
                break;
        }
    }

    private static void ConvertChildren(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        foreach (var child in node.ChildNodes)
        {
            ConvertNode(
                child,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);
        }
    }

    private static bool LooksLikeCodeBlock(HtmlNode node)
    {
        var lineBreakCount = node
            .Descendants("br")
            .Count();

        if (lineBreakCount < 2)
            return false;

        var hasColoredSyntax = node
            .Descendants("span")
            .Any(span => span.GetAttributeValue("style", string.Empty).Contains(
                "color",
                StringComparison.OrdinalIgnoreCase) == true);

        if (hasColoredSyntax)
            return true;

        var text = WebUtility.HtmlDecode(node.InnerText);

        return text.Contains('{') &&
               text.Contains('}') &&
               (text.Contains(';') ||
                text.Contains("public ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("private ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("class ", StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendCodeBlock(
        HtmlNode node,
        StringBuilder builder,
        string language)
    {
        var code = new StringBuilder();
        AppendCodeText(node, code);

        var normalizedCode = NormalizeCodeText(code.ToString());
        if (string.IsNullOrWhiteSpace(normalizedCode))
            return;

        EnsureLineStart(builder);
        builder.Append("```");
        builder.Append(language);
        builder.Append('\n');
        builder.Append(normalizedCode);
        builder.Append('\n');
        builder.Append("```\n\n");
    }

    private static void AppendCodeText(
        HtmlNode node,
        StringBuilder builder)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            builder.Append(node.InnerText);
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
            return;

        if (string.Equals(node.Name, "br", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('\n');
            return;
        }

        foreach (var child in node.ChildNodes)
            AppendCodeText(child, builder);
    }

    private static string NormalizeCodeText(string code)
    {
        code = WebUtility.HtmlDecode(code)
            .Replace('\u00A0', ' ')
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var lines = code.Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join('\n', lines);
    }

    private static string GetCodeLanguage(HtmlNode node)
    {
        var className = node.GetAttributeValue("class", string.Empty);

        if (className.Contains("csharp", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("cs", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        var text = WebUtility.HtmlDecode(node.InnerText);

        if (text.Contains("CreateChildControls", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("HtmlTextWriter", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("protected override", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("private ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("public ", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        return "text";
    }

    private static void AppendHeading(
        HtmlNode node,
        StringBuilder builder,
        int level,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        EnsureLineStart(builder);

        builder.Append(
            new string('#', level));

        builder.Append(' ');

        ConvertChildren(
            node,
            builder,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory,
            listDepth);

        AppendParagraphBreak(builder);
    }

    private static void AppendInlineWrapper(
        HtmlNode node,
        StringBuilder builder,
        string marker,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        builder.Append(marker);

        ConvertChildren(
            node,
            builder,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory,
            listDepth);

        builder.Append(marker);
    }

    private static void AppendLink(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        var href = node.GetAttributeValue(
            "href",
            null);

        if (string.IsNullOrWhiteSpace(href))
        {
            ConvertChildren(
                node,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);

            return;
        }

        var text = GetVisibleText(node);

        if (string.IsNullOrWhiteSpace(text))
        {
            ConvertChildren(
                node,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);

            return;
        }

        builder.Append('[');
        builder.Append(text);
        builder.Append("](");
        builder.Append(
            NormalizePublishedUrl(
                href.Trim()));
        builder.Append(')');
    }

    private static void AppendImage(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory)
    {
        var src = node.GetAttributeValue(
            "src",
            null);

        if (string.IsNullOrWhiteSpace(src))
            return;

        var alt = node.GetAttributeValue(
            "alt",
            string.Empty);

        var imageUrl = GetImageMarkdownUrl(
            src,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory);

        builder.Append("![");
        builder.Append(alt);
        builder.Append("](");
        builder.Append(imageUrl);
        builder.Append(')');
    }

    private static string GetImageMarkdownUrl(
        string sourceUrl,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory)
    {
        if (!imagesBySourceUrl.TryGetValue(
                sourceUrl,
                out var image))
        {
            return sourceUrl;
        }

        if (!string.IsNullOrWhiteSpace(image.LinkedImageFileName))
        {
            var linkedLocalPath = ResolveLocalImagePath(
                imagePathPrefix,
                imagesDirectory,
                image.LinkedImageFileName);

            if (linkedLocalPath is not null)
            {
                return ToMarkdownPath(
                    Path.Combine(
                        imagePathPrefix,
                        image.LinkedImageFileName));
            }
        }

        if (!string.IsNullOrWhiteSpace(image.FileName))
        {
            var sourceLocalPath = ResolveLocalImagePath(
                imagePathPrefix,
                imagesDirectory,
                image.FileName);

            if (sourceLocalPath is not null)
            {
                return ToMarkdownPath(
                    Path.Combine(
                        imagePathPrefix,
                        image.FileName));
            }
        }

        return NormalizePublishedUrl(sourceUrl);
    }

    private static string NormalizePublishedUrl(string url)
    {
        url = WebUtility.HtmlDecode(url.Trim());

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("web.archive.org", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        const string marker = "/web/";
        var markerIndex = url.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
            return url;

        var remainder = url[(markerIndex + marker.Length)..];
        var separatorIndex = remainder.IndexOf('/');

        if (separatorIndex < 0)
            return url;

        var originalUrl = remainder[(separatorIndex + 1)..];

        return originalUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               originalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(originalUrl)
            : url;
    }

    private static string? ResolveLocalImagePath(
        string imagePathPrefix,
        string? imagesDirectory,
        string fileName)
    {
        if (!string.IsNullOrWhiteSpace(imagesDirectory))
        {
            var fullPath = Path.Combine(
                imagesDirectory,
                fileName);

            return File.Exists(fullPath)
                ? fullPath
                : null;
        }

        var relativePath = Path.Combine(
            imagePathPrefix,
            fileName);

        return File.Exists(relativePath)
            ? relativePath
            : null;
    }

    private static string ToMarkdownPath(string path)
    {
        var normalized = path.Replace(
            Path.DirectorySeparatorChar,
            '/');

        var segments = normalized
            .Split(
                '/',
                StringSplitOptions.None)
            .Select(segment =>
                segment switch
                {
                    "" => string.Empty,
                    "." => ".",
                    ".." => "..",
                    _ => Uri.EscapeDataString(segment)
                });

        return string.Join(
            '/',
            segments);
    }

    private static void ConvertList(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth,
        bool ordered)
    {
        var index = 1;

        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element ||
                !string.Equals(
                    child.Name,
                    "li",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureLineStart(builder);

            builder.Append(
                new string(' ', listDepth * 2));

            builder.Append(
                ordered
                    ? $"{index}. "
                    : "- ");

            ConvertListItem(
                child,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);

            builder.Append('\n');

            index++;
        }

        builder.Append('\n');
    }

    private static void ConvertListItem(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Element &&
                (string.Equals(
                     child.Name,
                     "ul",
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     child.Name,
                     "ol",
                     StringComparison.OrdinalIgnoreCase)))
            {
                builder.Append('\n');

                ConvertList(
                    child,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth + 1,
                    string.Equals(
                        child.Name,
                        "ol",
                        StringComparison.OrdinalIgnoreCase));

                continue;
            }

            ConvertNode(
                child,
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);
        }
    }

    private static void AppendBlockquote(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        var inner = new StringBuilder();

        ConvertChildren(
            node,
            inner,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory,
            listDepth);

        var lines = NormalizeMarkdown(
                inner.ToString())
            .Split('\n');

        EnsureLineStart(builder);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            builder.Append("> ");
            builder.Append(line);
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    private static string GetVisibleText(HtmlNode node)
    {
        return NormalizeInlineText(
            WebUtility
                .HtmlDecode(node.InnerText))
            .Trim();
    }

    private static void AppendText(
        StringBuilder builder,
        string text)
    {
        text = NormalizeInlineText(
            WebUtility
                .HtmlDecode(text));

        if (string.IsNullOrWhiteSpace(text))
        {
            if (builder.Length > 0 &&
                builder[^1] != '\n')
            {
                builder.Append(' ');
            }

            return;
        }

        builder.Append(text);
    }

    private static string NormalizeInlineText(string text)
    {
        text = text.Replace('\u00A0', ' ');

        var normalized = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\s+",
            " ");

        return normalized;
    }

    private static void AppendParagraphBreak(
        StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        while (builder.Length > 0 &&
               builder[^1] == '\n')
        {
            builder.Length--;
        }

        builder.Append("\n\n");
    }

    private static void EnsureLineStart(
        StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        if (builder[^1] != '\n')
            builder.Append('\n');
    }

    private static string NormalizeMarkdown(
        string markdown)
    {
        var lines = markdown
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        var result = new List<string>();
        var blankLine = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!blankLine)
                {
                    result.Add(string.Empty);
                    blankLine = true;
                }

                continue;
            }

            result.Add(line);
            blankLine = false;
        }

        return string.Join(
            Environment.NewLine,
            result).Trim();
    }
}


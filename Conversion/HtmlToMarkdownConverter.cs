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

                if (LooksLikeUnformattedCodeSample(node))
                {
                    AppendUnformattedCodeSample(
                        node,
                        builder);
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
                // Two trailing spaces make this an explicit Markdown line break, not a soft wrap.
                builder.Append("  \n");
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

            case "table":
                ConvertTable(
                    node,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
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
        // Require multiple lines and explicit syntax coloring so code-like examples in prose stay plain.
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

        return false;
    }

    private static bool LooksLikeUnformattedCodeSample(HtmlNode node)
    {
        if (node.Descendants("br").Count() < 2)
            return false;

        var text = WebUtility.HtmlDecode(node.InnerText);

        return text.Contains('{') &&
               text.Contains('}') &&
               (text.Contains(';') ||
                text.Contains("public ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("private ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("class ", StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendUnformattedCodeSample(
        HtmlNode node,
        StringBuilder builder)
    {
        var code = new StringBuilder();
        AppendCodeText(node, code);

        var lines = NormalizeCodeText(code.ToString())
            .Split('\n');

        // Blank lines preserve visible breaks in Markdown without adding a code fence.
        EnsureLineStart(builder);
        builder.Append(string.Join("\n\n", lines));
        AppendParagraphBreak(builder);
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

        if (text.Contains("<asp:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<%@", StringComparison.Ordinal))
        {
            return "html";
        }

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
        var content = new StringBuilder();
        ConvertChildren(
            node,
            content,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory,
            listDepth);

        var value = content.ToString();
        var trailingBreaks = new StringBuilder();
        while (value.EndsWith("  \n", StringComparison.Ordinal))
        {
            value = value[..^3];
            trailingBreaks.Insert(0, "  \n");
        }

        var leadingWhitespaceLength = value.Length - value.TrimStart().Length;
        var trailingWhitespaceStart = value.TrimEnd().Length;
        var leadingWhitespace = value[..leadingWhitespaceLength];
        var trailingWhitespace = value[trailingWhitespaceStart..];
        value = value.Trim();

        // Close Markdown emphasis before trailing hard breaks so the break stays outside the wrapper.
        builder.Append(leadingWhitespace);
        builder.Append(marker);
        builder.Append(value);
        builder.Append(marker);
        builder.Append(trailingWhitespace);
        builder.Append(trailingBreaks);
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
            var image = node.Descendants("img").FirstOrDefault();
            if (image is not null)
            {
                // Preserve destinations on image-only links, including archived video thumbnails.
                builder.Append('[');
                AppendImage(
                    image,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory);
                builder.Append("](");
                builder.Append(NormalizePublishedUrl(href.Trim()));
                builder.Append(')');
                return;
            }

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

    private static void ConvertTable(
        HtmlNode node,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        // Nested tables are converted separately; this table handles only its own rows.
        var rows = node
            .Descendants("tr")
            .Where(row => ReferenceEquals(
                row.Ancestors("table").FirstOrDefault(),
                node))
            .ToList();

        if (rows.Count == 0)
            return;

        var cellsByRow = rows
            .Select(row => row.ChildNodes
                .Where(child => child.NodeType == HtmlNodeType.Element &&
                                (child.Name.Equals("td", StringComparison.OrdinalIgnoreCase) ||
                                 child.Name.Equals("th", StringComparison.OrdinalIgnoreCase)))
                .ToList())
            .ToList();

        // A single row of linked entries reads better as a list than as a wide table.
        if (LooksLikeLinkedRoster(rows, cellsByRow))
        {
            AppendLinkedRoster(
                cellsByRow[0],
                builder,
                imagesBySourceUrl,
                imagePathPrefix,
                imagesDirectory,
                listDepth);
            return;
        }

        // Legacy posts often use one-cell tables for layout, not tabular data.
        if (cellsByRow.All(cells => cells.Count <= 1))
        {
            foreach (var cell in cellsByRow.SelectMany(cells => cells))
            {
                ConvertChildren(
                    cell,
                    builder,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
            }

            AppendParagraphBreak(builder);
            return;
        }

        var tableRows = cellsByRow
            .Select(cells => new
            {
                Cells = cells,
                Values = cells
                    .Select(cell => ConvertTableCell(
                        cell,
                        imagesBySourceUrl,
                        imagePathPrefix,
                        imagesDirectory,
                        listDepth))
                    .ToList()
            })
            // Malformed legacy tables can leave empty rows that should not appear in Markdown.
            .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();

        if (tableRows.Count == 0)
            return;

        var columnCount = tableRows.Max(row => row.Values.Count);
        // Treat bold first-row cells like headers when older HTML uses td instead of th.
        var hasHeader = tableRows[0].Cells.Any(cell =>
            cell.Name.Equals("th", StringComparison.OrdinalIgnoreCase)) ||
                        LooksLikeFormattedHeaderRow(tableRows[0].Cells);

        EnsureLineStart(builder);

        if (!hasHeader)
        {
            // Markdown tables require a header separator, so use an empty header row when needed.
            AppendMarkdownTableRow(
                builder,
                Enumerable.Repeat(string.Empty, columnCount).ToList());
            AppendMarkdownTableRow(
                builder,
                Enumerable.Repeat("---", columnCount).ToList());
        }

        for (var rowIndex = 0; rowIndex < tableRows.Count; rowIndex++)
        {
            var values = tableRows[rowIndex].Values;
            values.AddRange(Enumerable.Repeat(
                string.Empty,
                columnCount - values.Count));

            if (hasHeader && rowIndex == 0)
            {
                AppendMarkdownTableRow(builder, values);
                AppendMarkdownTableRow(
                    builder,
                    Enumerable.Repeat("---", columnCount).ToList());
            }
            else
            {
                AppendMarkdownTableRow(builder, values);
            }
        }

        AppendParagraphBreak(builder);
    }

    private static bool LooksLikeLinkedRoster(
        IReadOnlyList<HtmlNode> rows,
        IReadOnlyList<List<HtmlNode>> cellsByRow)
    {
        if (rows.Count != 1 || cellsByRow[0].Count < 2)
            return false;

        return cellsByRow[0].All(cell =>
            cell.Name.Equals("td", StringComparison.OrdinalIgnoreCase) &&
            cell.ChildNodes.Count(child => child.Name.Equals("br", StringComparison.OrdinalIgnoreCase)) >= 2 &&
            cell.Descendants("a").Count() >= 2);
    }

    private static bool LooksLikeFormattedHeaderRow(
        IReadOnlyList<HtmlNode> cells)
    {
        return cells.Count > 0 && cells.All(cell =>
        {
            var elements = cell.ChildNodes
                .Where(child => child.NodeType == HtmlNodeType.Element)
                .ToList();

            return elements.Count > 0 &&
                   elements.All(child =>
                       child.Name.Equals("strong", StringComparison.OrdinalIgnoreCase) ||
                       child.Name.Equals("b", StringComparison.OrdinalIgnoreCase));
        });
    }

    private static void AppendLinkedRoster(
        IReadOnlyList<HtmlNode> cells,
        StringBuilder builder,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        foreach (var cell in cells)
        {
            var item = new StringBuilder();

            foreach (var child in cell.ChildNodes)
            {
                if (child.Name.Equals("br", StringComparison.OrdinalIgnoreCase))
                {
                    AppendRosterItem(item, builder, listDepth);
                    continue;
                }

                ConvertNode(
                    child,
                    item,
                    imagesBySourceUrl,
                    imagePathPrefix,
                    imagesDirectory,
                    listDepth);
            }

            AppendRosterItem(item, builder, listDepth);
        }

        AppendParagraphBreak(builder);
    }

    private static void AppendRosterItem(
        StringBuilder item,
        StringBuilder builder,
        int listDepth)
    {
        var value = item.ToString().Trim();
        item.Clear();

        if (string.IsNullOrWhiteSpace(value))
            return;

        EnsureLineStart(builder);
        builder.Append(new string(' ', listDepth * 2));
        builder.Append("- ");
        builder.Append(value);
        builder.Append('\n');
    }

    private static string ConvertTableCell(
        HtmlNode cell,
        IReadOnlyDictionary<string, RecoveredImage> imagesBySourceUrl,
        string imagePathPrefix,
        string? imagesDirectory,
        int listDepth)
    {
        var content = new StringBuilder();
        ConvertChildren(
            cell,
            content,
            imagesBySourceUrl,
            imagePathPrefix,
            imagesDirectory,
            listDepth);

        // Keep cell line breaks inline so they do not split the surrounding Markdown table row.
        return NormalizeMarkdown(content.ToString())
            .Replace("  \n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
    }

    private static void AppendMarkdownTableRow(
        StringBuilder builder,
        IReadOnlyList<string> cells)
    {
        builder.Append("| ");
        builder.Append(string.Join(" | ", cells));
        builder.Append(" |\n");
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
            .Select(line => line.EndsWith("  ", StringComparison.Ordinal)
                ? line
                : line.TrimEnd())
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

        return string.Join("\n", result).Trim();
    }
}


using System.Net;
using System.Text.Json;
using HtmlAgilityPack;

namespace SchwammyRecovery.Extraction;

public sealed class CodeAnalysisDocument
{
    public bool HasPotentialCode { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public List<CodeBlockAnalysis> Blocks { get; init; } = [];
}

public sealed class CodeBlockAnalysis
{
    public int Index { get; init; }
    public string SourceElement { get; init; } = string.Empty;
    public string SuggestedLanguage { get; init; } = "text";
    public double CodeConfidence { get; init; }
    public double LanguageConfidence { get; init; }
    public List<string> Signals { get; init; } = [];
    public string Preview { get; init; } = string.Empty;
}

public interface ICodeAnalysisExtractor
{
    Task AnalyzeAsync(
        string slug,
        CancellationToken cancellationToken = default);
}

public sealed class CodeAnalysisExtractor : ICodeAnalysisExtractor
{
    private readonly string _outputDirectory;

    public CodeAnalysisExtractor(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
    }

    public async Task AnalyzeAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var postDirectory = Path.Combine(_outputDirectory, "extracted", slug);
        var contentPath = Path.Combine(postDirectory, "content.html");
        var analysisPath = Path.Combine(postDirectory, "code-analysis.json");

        if (!File.Exists(contentPath))
            return;

        var html = await File.ReadAllTextAsync(contentPath, cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var blocks = new List<CodeBlockAnalysis>();
        var index = 1;

        foreach (var pre in document.DocumentNode.SelectNodes("//pre")?.ToList() ?? [])
        {
            blocks.Add(AnalyzeBlock(pre, index++, "pre"));
        }

        foreach (var paragraph in document.DocumentNode.SelectNodes("//p")?.ToList() ?? [])
        {
            if (LooksLikeLegacyCodeParagraph(paragraph))
                blocks.Add(AnalyzeBlock(paragraph, index++, "p"));
        }

        var result = new CodeAnalysisDocument
        {
            HasPotentialCode = blocks.Count > 0,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Blocks = blocks
        };

        Directory.CreateDirectory(postDirectory);
        await File.WriteAllTextAsync(
            analysisPath,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static CodeBlockAnalysis AnalyzeBlock(
        HtmlNode node,
        int index,
        string sourceElement)
    {
        var code = ExtractCodeText(node);
        var decoded = WebUtility.HtmlDecode(code);
        var signals = new List<string>();
        var score = 0.0;

        if (sourceElement == "pre")
        {
            signals.Add("pre-element");
            score += 0.45;
        }

        if (node.GetAttributeValue("class", string.Empty)
                .Contains("code", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("code-class");
            score += 0.15;
        }

        if (sourceElement == "p")
        {
            signals.Add("legacy-formatted-paragraph");
            score += 0.25;
        }

        var coloredSpanCount = node
            .Descendants("span")
            .Count(span => span.GetAttributeValue("style", string.Empty)
                .Contains("color", StringComparison.OrdinalIgnoreCase));

        if (coloredSpanCount > 0)
        {
            signals.Add("colored-syntax");
            score += 0.20;
        }

        if (decoded.Contains('{') || decoded.Contains('}') || decoded.Contains(';'))
        {
            signals.Add("code-punctuation");
            score += 0.10;
        }

        var language = DetectLanguage(decoded, signals, out var languageConfidence);

        if (language != "text")
            score += 0.15;

        var lineCount = decoded.Split('\n').Length;

        if (lineCount > 1)
        {
            signals.Add("multiple-lines");
            score += 0.05;
        }

        return new CodeBlockAnalysis
        {
            Index = index,
            SourceElement = sourceElement,
            SuggestedLanguage = language,
            CodeConfidence = Math.Round(Math.Min(score, 1.0), 2),
            LanguageConfidence = Math.Round(languageConfidence, 2),
            Signals = signals,
            Preview = NormalizePreview(decoded)
        };
    }

    private static bool LooksLikeLegacyCodeParagraph(HtmlNode node)
    {
        if (node.Descendants("br").Count() < 2)
            return false;

        var text = WebUtility.HtmlDecode(node.InnerText);
        var hasColoredSyntax = node
            .Descendants("span")
            .Any(span => span.GetAttributeValue("style", string.Empty)
                .Contains("color", StringComparison.OrdinalIgnoreCase));

        return hasColoredSyntax ||
               (text.Contains('{') && text.Contains('}') &&
                (text.Contains(';') ||
                 text.Contains("public ", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("private ", StringComparison.OrdinalIgnoreCase)));
    }

    private static string DetectLanguage(
        string text,
        List<string> signals,
        out double confidence)
    {
        var csharpSignals = new[]
        {
            "public ", "private ", "protected ", "override ", "class ",
            "void ", "using ", "CreateChildControls", "HtmlTextWriter"
        };

        var csharpMatches = csharpSignals.Count(signal =>
            text.Contains(signal, StringComparison.OrdinalIgnoreCase));

        if (csharpMatches > 0)
        {
            signals.Add("csharp-keywords");
            confidence = Math.Min(0.55 + csharpMatches * 0.08, 0.95);
            return "csharp";
        }

        var hasAspNetMarkup = text.Contains("<asp:", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("<%@", StringComparison.Ordinal) && text.Contains("%>"));
        var hasMarkup = hasAspNetMarkup ||
            (text.Contains("</", StringComparison.Ordinal) && text.Contains("<", StringComparison.Ordinal));

        if (hasMarkup)
        {
            signals.Add(hasAspNetMarkup ? "aspnet-markup" : "html-markup");
            confidence = hasAspNetMarkup ? 0.9 : 0.8;
            return "html";
        }

        if (text.Contains("SELECT ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("FROM ", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("sql-keywords");
            confidence = 0.8;
            return "sql";
        }

        confidence = 0.25;
        return "text";
    }

    private static string ExtractCodeText(HtmlNode node)
    {
        var builder = new System.Text.StringBuilder();
        AppendNodeText(node, builder);
        return builder.ToString();
    }

    private static void AppendNodeText(HtmlNode node, System.Text.StringBuilder builder)
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
            AppendNodeText(child, builder);
    }

    private static string NormalizePreview(string text)
    {
        var normalized = text
            .Replace('\u00A0', ' ')
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim()
            .Replace("\n", "\\n", StringComparison.Ordinal);

        return normalized[..Math.Min(240, normalized.Length)];
    }
}
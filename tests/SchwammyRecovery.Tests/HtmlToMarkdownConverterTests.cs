using SchwammyRecovery.Conversion;
using SchwammyRecovery.Extraction;
using Xunit;

namespace SchwammyRecovery.Tests;

public sealed class HtmlToMarkdownConverterTests
{
    private readonly HtmlToMarkdownConverter _converter = new();

    [Fact]
    public void EmptyStrongElementDoesNotEmitMarkdownMarkersOrLoseLink()
    {
        var markdown = Convert(
            "<p><font><strong></strong></font>Here are the files: " +
            "<a href=\"https://example.com/files.zip\">files.zip</a></p>");

        Assert.Equal(
            "Here are the files: [files.zip](https://example.com/files.zip)",
            markdown);
    }

    [Fact]
    public void TrailingWhitespaceInsideStrongElementRemainsOutsideEmphasis()
    {
        var markdown = Convert(
            "<p>If you want a <strong>FREE COPY </strong>of this offer.</p>");

        Assert.Equal("If you want a **FREE COPY** of this offer.", markdown);
    }

    [Fact]
    public void CssStyledAnchorPreservesBoldAndUnderlineFormatting()
    {
        var namedAnchor = Convert(
            "<p><a style=\"FONT-WEIGHT: bold; COLOR: black; " +
            "TEXT-DECORATION: underline\" name=\"partners\">Our Partners:</a> " +
            "Code Camp is only possible thanks to our many partners.</p>");
        var linkedAnchor = Convert(
            "<a href=\"https://example.com\" " +
            "style=\"font-weight: bold; text-decoration: underline\">Partner</a>");

        Assert.Equal(
            "**<u>Our Partners:</u>** Code Camp is only possible thanks to our many partners.",
            namedAnchor);
        Assert.Equal(
            "[**<u>Partner</u>**](https://example.com/)",
            linkedAnchor);
    }

    [Fact]
    public void BreakInsideStrongElementRendersAfterTheBoldLabel()
    {
        var markdown = Convert(
            "<p><strong>Jean Barmash:<br></strong>Here are the details.</p>");

        Assert.Equal(
            "**Jean Barmash:**  \nHere are the details.",
            markdown);
    }

    [Fact]
    public void BlockquotePreservesParagraphBreaksBetweenChildParagraphs()
    {
        var markdown = Convert(
            "<blockquote><p>First paragraph.</p><p>Second paragraph.</p></blockquote>");

        Assert.Equal(
            "> First paragraph.\n>\n> Second paragraph.",
            markdown);
    }

    [Fact]
    public void NestedStrongAndEmphasisWithLinkedTextProducesBalancedMarkdown()
    {
        var markdown = Convert(
            "<p><strong><font size=\"3\"><em>Additional Door Prizes courtesy of:" +
            "&nbsp; Microsoft and</em> </font><font size=\"3\"><a " +
            "href=\"https://web.archive.org/web/20200930063006/http://www.wrox.com/\">" +
            "<em>Wrox Publishing</em></a></font><font size=\"3\"><em>.</em>" +
            "</font></strong></p>");

        Assert.Equal(
            "**_Additional Door Prizes courtesy of: Microsoft and_ " +
            "[_Wrox Publishing_](http://www.wrox.com/)<em>.</em>**",
            markdown);
    }

    [Fact]
    public void ItalicsBeginningWithPunctuationInsideBoldUseValidInlineMarkup()
    {
        var markdown = Convert(
            "<p><strong>Presenter: Aaron Shafer<i>, Lockheed Martin</i></strong></p>");

        Assert.Equal(
            "**Presenter: Aaron Shafer<em>, Lockheed Martin</em>**",
            markdown);
    }

    [Fact]
    public void BoldAndItalicWrappersNestInEitherOrder()
    {
        var boldOuter = Convert(
            "<p><strong>Bold <em>and italic</em></strong></p>");
        var italicOuter = Convert(
            "<p><em>Italic <strong>and bold</strong></em></p>");

        Assert.Equal("**Bold _and italic_**", boldOuter);
        Assert.Equal("_Italic **and bold**_", italicOuter);
    }

    [Fact]
    public void LinkLabelsPreserveNestedEmphasisAndInlineCode()
    {
        var formattedLink = Convert(
            "<p><strong><a href=\"https://example.com\"><em>Important</em> " +
            "link</a></strong></p>");
        var emphasizedLink = Convert(
            "<p><a href=\"https://example.com\"><strong>Important</strong> " +
            "link</a></p>");
        var codeInEmphasis = Convert(
            "<p><strong>Run <code>Build()</code> now</strong></p>");

        Assert.Equal("**[_Important_ link](https://example.com/)**", formattedLink);
        Assert.Equal("[**Important** link](https://example.com/)", emphasizedLink);
        Assert.Equal("**Run `Build()` now**", codeInEmphasis);
    }

    [Fact]
    public void UnformattedCodeLikeParagraphPreservesLinesWithoutFence()
    {
        var markdown = Convert(
            "<p>public void Demo()<br>{<br>Run();<br>}</p>");

        Assert.Equal(
            "public void Demo()\n\n{\n\nRun();\n\n}",
            markdown);
        Assert.DoesNotContain("```", markdown);
    }

    [Fact]
    public void SyntaxColoredCodeParagraphUsesCsharpFence()
    {
        var markdown = Convert(
            "<p><span style=\"color: blue\">public</span> void Demo()" +
            "<br>{<br>Run();<br>}</p>");

        Assert.Contains("```csharp", markdown);
        Assert.Contains("public void Demo()", markdown);
    }

    [Fact]
    public void HeaderlessTableUsesMarkdownHeaderSeparator()
    {
        var markdown = Convert(
            "<table><tr><td>Topic</td><td>Speaker</td></tr></table>");

        Assert.Contains("|  |  |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| Topic | Speaker |", markdown);
    }

    [Fact]
    public void HeaderlessLinkedNameValueTableBecomesLabeledBullets()
    {
        var markdown = Convert(
            "<table><tr><td><a href=\"https://example.com/a\">Apress</a></td>" +
            "<td>Books and shirts</td></tr>" +
            "<tr><td><a href=\"https://example.com/b\">Wrox</a></td>" +
            "<td>Books and water bottles</td></tr>" +
            "<tr><td>&nbsp;</td><td>&nbsp;</td></tr></table>");

        Assert.Contains("- **[Apress](https://example.com/a)**: Books and shirts", markdown);
        Assert.Contains("- **[Wrox](https://example.com/b)**: Books and water bottles", markdown);
        Assert.DoesNotContain("| --- | --- |", markdown);
    }

    [Fact]
    public void OneColumnLayoutWrapperPreservesNestedTableImages()
    {
        var markdown = Convert(
            "<table><tr><td><p>Gallery</p><table><tr>" +
            "<td><img src=\"https://example.com/one.jpg\" alt=\"One\"></td>" +
            "<td><img src=\"https://example.com/two.jpg\" alt=\"Two\"></td>" +
            "</tr></table></td></tr></table>");

        Assert.Contains("![One](https://example.com/one.jpg)", markdown);
        Assert.Contains("![Two](https://example.com/two.jpg)", markdown);
    }

    [Fact]
    public void NestedDataTableInsideLayoutTableRemainsARealMarkdownTable()
    {
        var markdown = Convert(
            "<table><tr><td><table><tr>" +
            "<td><strong>Company</strong></td><td><strong>Prizes Provided</strong></td>" +
            "</tr><tr><td>Apress</td><td>Books</td></tr></table></td>" +
            "<td><img src=\"https://example.com/prize.jpg\" alt=\"Prize\"></td>" +
            "</tr></table>");

        Assert.Contains("| **Company** | **Prizes Provided** |", markdown);
        Assert.Contains("| Apress | Books |", markdown);
        Assert.DoesNotContain("\\| **Company**", markdown);
        Assert.Contains("![Prize](https://example.com/prize.jpg)", markdown);
    }

    [Fact]
    public void ImageOnlyLinkPreservesItsDestination()
    {
        var markdown = Convert(
            "<p><a href=\"https://video.example/watch\"><img " +
            "src=\"https://images.example/thumbnail.jpg\" alt=\"\"></a></p>");

        Assert.Contains(
            "[![](https://images.example/thumbnail.jpg)](https://video.example/watch)",
            markdown);
    }

    [Fact]
    public void FloatAlignedLinkedImageMovesAfterParagraphWithoutSplittingText()
    {
        var markdown = Convert(
            "<p>Intro <a href=\"https://example.com/photo\"><img " +
            "src=\"https://images.example/photo.jpg\" alt=\"Photo\" align=\"left\"></a>" +
            "continues text.</p>");

        Assert.Equal(
            "Intro continues text.\n\n[![Photo](https://images.example/photo.jpg)]" +
            "(https://example.com/photo)",
            markdown);
    }

    [Fact]
    public void ImageLinkUrlsEncodeSpacesForMarkdown()
    {
        var markdown = Convert(
            "<a href=\"https://example.com/Getting Food_2.jpg\"><img " +
            "src=\"https://example.com/Getting Food_thumb.jpg\" alt=\"Getting Food\"></a>");

        Assert.Equal(
            "[![Getting Food](https://example.com/Getting%20Food_thumb.jpg)]" +
            "(https://example.com/Getting%20Food_2.jpg)",
            markdown);
    }

    [Fact]
    public void LayoutTableContainingBlockquotePreservesQuoteFormatting()
    {
        var markdown = Convert(
            "<table><tr><td><blockquote><p>First quote.</p><p>Second quote.</p>" +
            "</blockquote></td><td><img src=\"https://example.com/photo.jpg\" " +
            "alt=\"Photo\"></td></tr></table>");

        Assert.Contains("> First quote.\n>\n> Second quote.", markdown);
        Assert.Contains("![Photo](https://example.com/photo.jpg)", markdown);
        Assert.DoesNotContain("| --- | --- |", markdown);
    }

    [Fact]
    public void OneColumnLayoutRowsAreSeparated()
    {
        var markdown = Convert(
            "<table><tr><td><strong>Silver Partners</strong></td></tr>" +
            "<tr><td><img src=\"https://example.com/logo.png\" alt=\"Partner\"></td></tr></table>");

        Assert.Contains("**Silver Partners**\n\n![Partner]", markdown);
    }

    [Fact]
    public void SiblingNestedListPreservesLinksUnderCategory()
    {
        var markdown = Convert(
            "<ul><li>Developer Blogs</li><ul><li>" +
            "<a href=\"https://example.com/blog\">Example Blog</a>" +
            "</li></ul></ul>");

        Assert.Contains("- Developer Blogs", markdown);
        Assert.Contains("  - [Example Blog](https://example.com/blog)", markdown);
    }

    [Fact]
    public void UnorderedListWrapperPreservesParagraphChildrenWithoutListItems()
    {
        var markdown = Convert(
            "<ul><p><strong>Alt.NET<br></strong>Brian Donahue - Panel Discussion" +
            "<br>David Laribee - Domain-Driven Design</p>" +
            "<p><strong>Architecture<br></strong>Mitch Ruebush - Overview</p></ul>");

        Assert.Contains("**Alt.NET**  \nBrian Donahue - Panel Discussion", markdown);
        Assert.Contains("David Laribee - Domain-Driven Design", markdown);
        Assert.Contains("**Architecture**  \nMitch Ruebush - Overview", markdown);
    }

    [Fact]
    public void PreformattedCsharpAndAspNetSamplesKeepLanguageLabels()
    {
        var csharp = Convert(
            "<pre class=\"code\">protected override void CreateChildControls() { }</pre>");
        var aspNet = Convert(
            "<pre>&lt;asp:Content&gt;body&lt;/asp:Content&gt;</pre>");

        Assert.Contains("```csharp", csharp);
        Assert.Contains("```html", aspNet);
    }

    [Fact]
    public void ListViewCodeSamplesInferCsharpFromFrameworkTypesAndProtectedMethods()
    {
        var itemType = Convert(
            "<pre class=\"code\"><span style=\"color: blue\">if</span> " +
            "(e.Item.ItemType == <span style=\"color: teal\">ListViewItemType</span>.DataItem) " +
            "{ }</pre>");
        var eventHandler = Convert(
            "<pre class=\"code\"><span style=\"color: blue\">protected</span> " +
            "<span style=\"color: blue\">void</span> ListView1_ItemDataBound(" +
            "<span style=\"color: blue\">object</span> sender, " +
            "<span style=\"color: teal\">ListViewItemEventArgs</span> e) { }</pre>");

        Assert.StartsWith("```csharp\n", itemType);
        Assert.StartsWith("```csharp\n", eventHandler);
    }

    [Theory]
    [InlineData("namespace Example")]
    [InlineData("class Example")]
    [InlineData("void Run()")]
    [InlineData("return true;")]
    [InlineData("if (value == 1)")]
    public void CommonCsharpIdentifiersAndSyntaxInferLanguage(string source)
    {
        var markdown = Convert($"<pre class=\"code\">{source}</pre>");

        Assert.StartsWith("```csharp\n", markdown);
    }

    [Fact]
    public void BracesAloneDoNotInferCsharpForJson()
    {
        var markdown = Convert(
            "<pre class=\"code\">{ &quot;name&quot;: &quot;Example&quot; }</pre>");

        Assert.StartsWith("```text\n", markdown);
    }

    [Fact]
    public void ColoredDateTimeCodeFragmentUsesCsharpFence()
    {
        var markdown = Convert(
            "<pre class=\"code\"><span style=\"color: rgb(43,145,175)\">DateTime</span> myDate; " +
            "<span style=\"color: rgb(0,0,255)\">if</span> (myDate.IsMyBirthday()) " +
            "<span style=\"color: rgb(0,0,255)\">return</span> true;</pre>");

        Assert.StartsWith("```csharp\n", markdown);
        Assert.DoesNotContain("```text", markdown);
    }

    [Fact]
    public void PlainPreformattedIntroIsNotFencedAsCode()
    {
        var markdown = Convert(
            "<pre class=\"code\">or even:</pre>" +
            "<pre class=\"code\"><span style=\"color: rgb(43,145,175)\">DateTime</span> " +
            "myDate; <span style=\"color: rgb(0,0,255)\">if</span> " +
            "(myDate.IsMyBirthday()) <span style=\"color: rgb(0,0,255)\">return</span> true;</pre>");

        Assert.Contains("or even:\n\n```csharp", markdown);
        Assert.DoesNotContain("```text", markdown);
    }

    [Fact]
    public void ConsecutivePreLinesInMonospaceContainerBecomeOneInferredCodeBlock()
    {
        var markdown = Convert(
            "<div style=\"font-family: Consolas, 'Courier New', monospace\">" +
            "<pre>namespace Utilities</pre><pre>{</pre>" +
            "<pre>public static class DateExtension</pre><pre>{</pre>" +
            "</div>");

        Assert.StartsWith(
            "```csharp\nnamespace Utilities\n{\npublic static class DateExtension\n{\n```",
            markdown);
        Assert.Equal(2, markdown.Split("```", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void HeadingsInlineCodeAndOrdinaryListsKeepTheirMarkdown()
    {
        var markdown = Convert(
            "<h2>Features</h2><p>Call <code>Run()</code> when ready.</p>" +
            "<ol><li>First<ul><li>Nested</li></ul></li><li>Second</li></ol>");

        Assert.Contains("## Features", markdown);
        Assert.Contains("Call `Run()` when ready.", markdown);
        Assert.Contains("1. First", markdown);
        Assert.Contains("  - Nested", markdown);
        Assert.Contains("2. Second", markdown);
    }

    [Fact]
    public void HeaderCellsBecomeMarkdownTableHeaders()
    {
        var markdown = Convert(
            "<table><tr><th>Name</th><th>Role</th></tr>" +
            "<tr><td>Ada</td><td>Presenter</td></tr></table>");

        Assert.Contains("| Name | Role |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| Ada | Presenter |", markdown);
    }

    [Fact]
    public void LinkedRosterTableBecomesBulletedLinks()
    {
        var markdown = Convert(
            "<table><tr><td><a href=\"https://example.com/a\">A</a><br>" +
            "<a href=\"https://example.com/b\">B</a><br>" +
            "<a href=\"https://example.com/c\">C</a></td>" +
            "<td><a href=\"https://example.com/d\">D</a><br>" +
            "<a href=\"https://example.com/e\">E</a><br>" +
            "<a href=\"https://example.com/f\">F</a></td></tr></table>");

        Assert.Contains("- [A](https://example.com/a)", markdown);
        Assert.Contains("- [F](https://example.com/f)", markdown);
        Assert.DoesNotContain("| [A]", markdown);
    }

    [Fact]
    public void WaybackLinksNormalizeToOriginalUrls()
    {
        var markdown = Convert(
            "<a href=\"https://web.archive.org/web/20200101123456/" +
            "http://example.com/page?x=1&amp;y=2\">Original page</a>");

        Assert.Equal("[Original page](http://example.com/page?x=1&y=2)", markdown);
    }

    [Fact]
    public void LocalImagePathsEncodeFilenameSpaces()
    {
        var imagesDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(imagesDirectory);

        try
        {
            File.WriteAllBytes(
                Path.Combine(imagesDirectory, "My Photo.jpg"),
                Array.Empty<byte>());

            var markdown = _converter.Convert(
                "<img src=\"https://images.example/photo.jpg\" alt=\"Photo\">",
                [new RecoveredImage
                {
                    SourceUrl = "https://images.example/photo.jpg",
                    FileName = "My Photo.jpg"
                }],
                "../../images/test",
                imagesDirectory);

            Assert.Equal("![Photo](../../images/test/My%20Photo.jpg)", markdown);
        }
        finally
        {
            Directory.Delete(imagesDirectory, recursive: true);
        }
    }

    private string Convert(string html)
    {
        return _converter.Convert(
            html,
            Array.Empty<RecoveredImage>(),
            "../../images/test");
    }
}
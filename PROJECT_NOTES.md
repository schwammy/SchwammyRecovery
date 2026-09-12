# SchwammyRecovery Project Notes

## Goal
Recover archived WordPress posts from Wayback, extract content and images, download local copies, and convert recovered posts to Markdown for preview and later publishing.

## Current architecture
- `Program.cs`
  - wires up dependency injection and runs the menu-driven pipeline
- `Discovery/ArchiveCrawler.cs`
  - crawls a Wayback archive month/page and discovers original post URLs
- `Wayback/WaybackClient.cs`
  - fetches archived HTML from Wayback
- `Extraction/*`
  - extracts post content, comments, and image metadata
- `Download/ImageDownloader.cs`
  - downloads recovered images into `output/images/<slug>/`
- `Conversion/HtmlToMarkdownConverter.cs`
  - converts extracted HTML into Markdown and rewrites local image paths
- `Steps/*`
  - orchestrates each pipeline stage

## Current workflow
1. Discovery
   - runs `DiscoveryStep`
   - writes `output/discovery/post-urls.json`
2. Wayback recovery
   - recovers archived HTML for discovered posts
3. Extraction
   - extracts `content.html`, comments, and `images.json`
4. Image download
   - downloads recovered images into `output/images/<slug>/`
5. Markdown conversion
   - writes Markdown files to `output/markdown/<slug>/post.md`

## Important rules
- The discovery step writes one shared `post-urls.json`.
- If you change the archive month, delete the existing `output` tree first so old discovery/extraction/image/Markdown artifacts do not contaminate the new run.
- Step 5 intentionally skips existing Markdown files and only recreates them when the file is missing.
- Local image paths in generated Markdown must be URL-encoded for filenames with spaces or other special characters so VS Code Markdown preview can load them.

## Current verified behavior
- Markdown preview now works for local images when generated paths are encoded correctly.
- The converter currently emits local relative paths such as:
  - `../../images/<slug>/WindowsExperienceIndex5.jpg`
  - `../../images/<slug>/My%20Windows%20Experience%20Rating.jpg`
- The `vista-is-installed-and-working-after-a-few-bumps-in-the-road` post is a verified example of the encoded-path requirement.

## Recent useful targets
- Current discovery start URL is April 2007:
  - `http://www.schwammysays.net/2007/04/`
- The app should be run from the built output directory (`bin/Debug/net8.0`) for the local output workflow.

## Known gotchas
- `dotnet run` from the project root does not use the existing local `output` tree reliably for this workflow.
- The app’s interactive menu is the intended way to run stages locally.
- Generated Markdown files under `bin/Debug/net8.0/output/markdown/` are the real preview targets.

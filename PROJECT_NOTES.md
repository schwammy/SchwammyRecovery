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
- `Recovery/WaybackRecoveryService.cs`
  - owns recovery logic, archive-page caching, and fallback source normalization
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
   - caches archive pages for reuse in `output/archive-pages/...`
3. Extraction
   - extracts `content.html`, comments, and `images.json`
4. Image download
   - downloads recovered images into `output/images/<slug>/`
5. Markdown conversion
   - writes Markdown files to `output/markdown/<slug>/post.md`
   - also generates `output/markdown/index.md` as a clickable table of contents

## Project conventions and coding rules
- Keep interfaces and their primary implementations together in the same file/folder unless the interface intentionally has multiple implementations.
- Prefer small, single-responsibility services for logic and keep steps focused on orchestration.
- Keep DTOs and metadata classes in separate files when they are meaningful domain objects instead of nested local types.
- Add comments sparingly and only when they explain intent, non-obvious decisions, or external contracts that are not obvious from the code itself.
- When adding project conventions, update `PROJECT_NOTES.md` so the rules are preserved across future changes.
- The discovery step writes one shared `post-urls.json`.
- The app now keeps a persistent per-post status manifest in `output/post-status.json` that tracks discovery, recovery, extraction, image download, and Markdown conversion state.
- Before each step processes a post, it loads the stored status entry and skips posts whose current step is already marked successful, while leaving failed entries available for retry on a later run.
- If you change the archive month, delete the existing `output` tree first so old discovery/extraction/image/Markdown artifacts do not contaminate the new run.
- Step 5 intentionally skips existing Markdown files and only recreates them when the file is missing.
- Local image paths in generated Markdown must be URL-encoded for filenames with spaces or other special characters so VS Code Markdown preview can load them.

## Current verified behavior
- Markdown preview now works for local images when generated paths are encoded correctly.
- The converter currently emits local relative paths such as:
  - `../../images/<slug>/WindowsExperienceIndex5.jpg`
  - `../../images/<slug>/My%20Windows%20Experience%20Rating.jpg`
- The `vista-is-installed-and-working-after-a-few-bumps-in-the-road` post is a verified example of the encoded-path requirement.
- Local Markdown image paths were fixed by encoding path segments for filenames with spaces and other special characters.
- Step 5 now skips existing Markdown files and only recreates them when the file is missing.
- Discovery was switched to April 2007 for additional image-heavy content testing.
- The Vista post now previews correctly after regenerating the Markdown.
- Recovery now normalizes Wayback/archive-page URLs back to the original post URL before isolating the target article, which avoids saving full archive pages as `source.html`.

## Recent useful targets
- Current discovery start URL is April 2007:
  - `http://www.schwammysays.net/2007/04/`
- The app should be run from the built output directory (`bin/Debug/net8.0`) for the local output workflow.

## Known gotchas
- `dotnet run` from the project root does not use the existing local `output` tree reliably for this workflow.
- The app’s interactive menu is the intended way to run stages locally.
- Generated Markdown files under `bin/Debug/net8.0/output/markdown/` are the real preview targets.
- Recovery can fall back to archive pages when a post has no usable Wayback capture; in those cases the saved `source.html` must still be narrowed to the target post article.

## Current todo list
Use this as the working roadmap for future conversations and follow-up work. If a fix is deferred, add it here so the outstanding work stays visible.

### High priority
- Finish a full end-to-end validation pass across the discovered posts and confirm the remaining recovered outputs are correct, especially posts that rely on archive-page fallback instead of direct captures.
- Review the discovered-but-not-yet-recovered cases and decide which are expected gaps versus true defects that should be fixed in the recovery path.
- Audit the generated output tree for consistency after recovery/extraction/conversion, including provenance files, `source.html`, `content.html`, `images.json`, and `output/markdown/index.md`.
- Re-run the full pipeline after any recovery logic change and compare the resulting artifacts to make sure the fix did not regress earlier working posts.

### Medium priority
- Improve the README so new readers can understand the workflow, generated output tree, and expected local preview steps.
- Add a true Ghost export step once Markdown and image output are stable.
- Evaluate whether archive-page caching should expose more visible status/logging for troubleshooting and repeated runs.
- Review the generated `output/markdown/index.md` experience and improve how posts are grouped or labeled for easier browsing.
- Add an in-memory status cache for `post-status.json` so status updates are held in memory and persisted only when changes occur, reducing repeated read/write overhead during large runs.

### Nice to have
- Add richer post metadata or front matter to generated Markdown files.
- Improve the extraction pipeline to surface more useful summaries of recovered comments and image usage.
- Consider adding a small test suite around the most fragile recovery and conversion behaviors.

## Keep in sync
Update these notes whenever the architecture, conventions, workflow, or task list changes.

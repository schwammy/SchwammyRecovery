# SchwammyRecovery Project Notes

## Goal
Recover archived WordPress posts from Wayback, extract content and images, download local copies, and produce a portable Markdown export for preview and a separate blog project.

## Documentation ownership
- This file is the source of truth for project architecture, decisions, status, and todo priorities.
- `README.md` is the concise, user-facing project overview and implementation status.
- `.github/copilot-instructions.md` contains only repository-specific instructions for Copilot behavior and workflow.

## Publishing direction
- Keep Hugo site generation, blog content, and GitHub Actions deployment in a separate blog repository. This repository remains dedicated to Wayback recovery and portable export; do not add the blog site or deployment workflow here.
- Copy selected, publication-ready content from the portable Markdown export into the blog repository. Do not expose raw Wayback captures or recovery intermediates as part of the blog.
- The blog importer adds Hugo-only provenance metadata (for example, `archive_recovered: true`) to selected imported posts; a shared layout or partial uses that flag to show the archive-recovery notice. Do not add the notice or Hugo-specific metadata to recovery artifacts or portable exports.
- For posts confirmed unrecoverable after review, preserve a minimal stub with original identifying metadata and an engine-neutral status (for example, `recovery_status: unrecoverable`) in the portable export. The blog importer maps that status to Hugo-only stub metadata, and the shared layout displays a clear not-recovered notice. Do not stub posts that are still pending review or have transient recovery failures.
- Generate the public site with Hugo and deploy it with GitHub Actions to GitHub Pages from the separate blog repository.
- Use `schwammysays.net` as the intended root domain after the temporary Pages site has been reviewed. Do not change domain registration or DNS as part of local development; domain changes require explicit approval.
- Preserve historical post paths as closely as possible. Derive Hugo permalinks from recovered original URLs, then identify and map exceptions before pointing the domain.
- Keep the portable Markdown export and comment JSON sidecars as the local, engine-neutral source copies. Do not modify recovery or extraction artifacts while building the public site.
- Render recovered comments at the end of each post with a comments-closed notice. Use CommentBox.io for new posts only, subject to confirming its current pricing, moderation features, and comment export/portability.
- Use Google Analytics 4 for statistics. Account for its tracking script and any required privacy notice or consent behavior before enabling it publicly.
- A static client-side search index is required. Pagefind is the current candidate; selection and implementation are still pending.
- GitHub Pages is the initial low-cost host, not an irreversible platform choice. Reassess its terms and migrate hosts before enabling monetization if the site's plans no longer fit those terms.
- The separate Hugo blog and GitHub Actions deployment proof of concept were implemented and validated at the temporary GitHub Pages URL. The Pages site is currently unpublished; the production domain and DNS are unchanged.

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
1. Discover post URLs
  - runs `DiscoveryStep`; with no date input, the menu defaults to April 2007
   - writes `output/discovery/post-urls.json`
2. Recover Wayback HTML
   - recovers archived HTML for discovered posts
   - caches archive pages for reuse in `output/archive-pages/...`
3. Extract post content
  - extracts `content.html`, comments, `images.json`, and derived `code-analysis.json`
4. Recover images
  - downloads images for extracted posts into `output/images/<slug>/`
5. Convert to Markdown
   - writes Markdown files to `output/markdown/<slug>/post.md`
   - also generates `output/markdown/index.md` as a clickable table of contents
6. Review outstanding posts
  - reports discovered and recovered counts, outstanding posts, and missing image downloads/files
7. Create portable Markdown export
  - writes an engine-neutral bundle to `output/export/portable-markdown/`
  - adds YAML front matter, copies images into `assets/`, and preserves comments as JSON sidecars

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
- The portable export reads recovery and Markdown artifacts without modifying them.
- Code review metadata is written to `output/extracted/<slug>/code-analysis.json`; it is derived from `content.html` and does not modify recovery or extraction source artifacts.
- Step 3 regenerates derived comment JSON for completed posts, decoding repeated HTML entities and normalizing comment URLs out of Wayback captures.
- Export-local image paths use `../assets/<slug>/...` from each exported post.
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
- Portable Markdown export has been validated across 121 recovered posts.
- Export metadata HTML-decodes recovered values before writing YAML front matter.
- Markdown conversion preserves semantic `<pre>/<code>` blocks and legacy Visual Studio code paragraphs as fenced C# blocks with indentation intact.
- `custom-server-controls-createchildcontrols-or-render` is the large-code regression post; its generic `<pre class="code">` blocks are inferred as C# and export with `csharp` fences.
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
- Add an import script in the blog repository that copies explicitly selected portable-export posts and assets into Hugo content bundles, adds Hugo-only archive-provenance front matter to imported copies, previews planned changes, and prevents accidental overwrites; leave the portable export untouched.
- Implement the front-matter-driven archive-recovery notice in the Hugo layout and finalize its public wording.
- Represent posts confirmed unrecoverable after review as minimal portable-export stubs with original metadata and an engine-neutral recovery status; have the blog importer create corresponding Hugo stub posts and display a not-recovered notice. Keep pending posts and transient failures distinct.
- Audit recovered original URLs and configure Hugo permalinks and redirects to preserve legacy paths.
- Evaluate and implement a client-side search index (Pagefind is the current candidate).
- Add CommentBox.io for new posts only after checking plan terms, moderation, and comment export; keep historical comment rendering and JSON archives independent.
- Add Google Analytics 4 after deciding privacy notice and consent requirements.
- Review GitHub Pages policy before monetization; select and test a replacement host before any necessary move.
- Implement a disk-backed CDX query cache (e.g., in `output/cache/cdx/`) so Wayback capture index lookups can be reused indefinitely across pipeline runs without repeating slow network requests.
- Improve the README so new readers can understand the workflow, generated output tree, and expected local preview steps.
- Evaluate whether archive-page caching should expose more visible status/logging for troubleshooting and repeated runs.
- Review the generated `output/markdown/index.md` experience and improve how posts are grouped or labeled for easier browsing.
- Add an in-memory status cache for `post-status.json` so status updates are held in memory and persisted only when changes occur, reducing repeated read/write overhead during large runs.

### Nice to have
- Add richer post metadata or front matter to generated Markdown files.
- Improve the extraction pipeline to surface more useful summaries of recovered comments and image usage.
- Consider adding a small test suite around the most fragile recovery and conversion behaviors.

## Keep in sync
Update these notes whenever the architecture, conventions, workflow, or task list changes.

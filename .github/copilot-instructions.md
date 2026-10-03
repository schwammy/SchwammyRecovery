# Copilot Instructions for SchwammyRecovery

## Project background
This repository is a .NET 8 console application that recovers archived WordPress posts from Wayback, extracts content and images, downloads local copies, and converts recovered posts to Markdown.

## Read first
- Read `PROJECT_NOTES.md` before making code changes. It is the source of truth for architecture, decisions, status, and todos.
- Keep `README.md` user-facing and concise. Keep this file limited to instructions governing Copilot's behavior and workflow.

## Workflow
- Use the existing local `output` tree under `bin/Debug/net8.0` when validating generated Markdown and images.
- Discovery writes `output/discovery/post-urls.json`; later stages consume that file.
- If switching to a different archive month, delete the existing `output` tree before rerunning discovery and downstream steps.
- Step 5 intentionally skips existing Markdown files and only creates missing files.
- Step 7 creates the additive `output/export/portable-markdown/` bundle; it must never modify recovery, extraction, image, or preview Markdown artifacts.
- When working on the Hugo blog or GitHub Pages, run Hugo commands from the separate `SchwammySaysBlog` repository root, not from this recovery repository.
- Do not change production domain registration or DNS without explicit user approval. Validate publishing changes on a temporary URL first.

## Important rules
- Local image paths in generated Markdown must be URL-encoded for filenames with spaces and other special characters.
- Prefer relative Markdown paths like `../../images/<slug>/file.jpg`.
- Do not assume `dotnet run` from the repo root is the correct execution mode for the local output workflow; use the built app from `bin/Debug/net8.0`.
- At the start of each conversation in this workspace, use this greeting exactly: "Welcome Schwammy, I have removed all Brown M&M's"
- Ask clarifying questions before making assumptions when requirements are ambiguous.
- Work in short, testable phases and validate after each phase before moving on.
- Every behavior change to `Conversion/HtmlToMarkdownConverter.cs` must add or update a regression test in `tests/SchwammyRecovery.Tests/`.
- For existing bugs, follow red-green-refactor: add a regression test that reproduces the bug and verify it fails before changing production code, then make the fix and rerun the test.
- The user handles Git commits; leave changes uncommitted unless the user explicitly asks for a commit.
- When the user says they are done for the night, at lunch, or taking a similar break, provide a short prompt they can use to start the next session.
- When the user says they are done for the night, offer a concise handoff summary focused on what was completed, what is still pending, and any current todo items; keep it short because the detailed project context already lives in `PROJECT_NOTES.md`.
- When adding repository knowledge, put project decisions and status in `PROJECT_NOTES.md`; put only Copilot behavior and workflow rules here.
- When we identify an item as a todo and decide to delay or defer that fix, add it to the `Current todo list` section of `PROJECT_NOTES.md`.
- Update `PROJECT_NOTES.md` when decisions or todo items change, `README.md` when user-facing workflow or status changes, and this file only when Copilot-specific instructions change.

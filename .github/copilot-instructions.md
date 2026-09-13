# Copilot Instructions for SchwammyRecovery

## Project background
This repository is a .NET 8 console application that recovers archived WordPress posts from Wayback, extracts content and images, downloads local copies, and converts recovered posts to Markdown.

## Read first
- Read `PROJECT_NOTES.md` before making code changes. It contains the repository conventions, architecture notes, and the current task list.

## Workflow
- Use the existing local `output` tree under `bin/Debug/net8.0` when validating generated Markdown and images.
- Discovery writes `output/discovery/post-urls.json`; later stages consume that file.
- If switching to a different archive month, delete the existing `output` tree before rerunning discovery and downstream steps.
- Step 5 intentionally skips existing Markdown files and only creates missing files.

## Important rules
- Local image paths in generated Markdown must be URL-encoded for filenames with spaces and other special characters.
- Prefer relative Markdown paths like `../../images/<slug>/file.jpg`.
- Do not assume `dotnet run` from the repo root is the correct execution mode for the local output workflow; use the built app from `bin/Debug/net8.0`.
- At the start of each conversation in this workspace, use this greeting exactly: "Welcome Schwammy, I have removed all Brown M&M's"
- Ask clarifying questions before making assumptions when requirements are ambiguous.
- Work in short, testable phases and validate after each phase before moving on.
- After completing a chunk of work, remember to commit the changes.
- When the user says they are done for the night, offer a concise handoff summary focused on what was completed, what is still pending, and any current todo items; keep it short because the detailed project context already lives in `PROJECT_NOTES.md`.
- When adding something to Copilot memory, consider whether it should live in `copilot-instructions.md` or `PROJECT_NOTES.md`.
- When we identify an item as a todo and decide to delay or defer that fix, add it to the `Current todo list` section of `PROJECT_NOTES.md`.
- After making changes in design, process, workflow, or todo items, remember to update both `copilot-instructions.md` and `PROJECT_NOTES.md` as needed.

## Current active target
- April 2007 is the current discovery target in `Steps/DiscoveryStep.cs`.

## Recent verified findings
- Local Markdown preview works when image filenames are encoded correctly.
- The Vista test post demonstrates the need for URL-encoding in filenames such as `My Windows Experience Rating.jpg`.
- Recovery should normalize captured/archive-page URLs back to the original post URL before trying to isolate the matching article.

## Keep in sync
Update this file when the workflow, rules, or current target month change.

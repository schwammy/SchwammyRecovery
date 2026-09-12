# Copilot Instructions for SchwammyRecovery

## Project background
This repository is a .NET 8 console application that recovers archived WordPress posts from Wayback, extracts content and images, downloads local copies, and converts recovered posts to Markdown.

## Workflow
- Use the existing local `output` tree under `bin/Debug/net8.0` when validating generated Markdown and images.
- Discovery writes `output/discovery/post-urls.json`; later stages consume that file.
- If switching to a different archive month, delete the existing `output` tree before rerunning discovery and downstream steps.
- Step 5 intentionally skips existing Markdown files and only creates missing files.

## Important rules
- Local image paths in generated Markdown must be URL-encoded for filenames with spaces and other special characters.
- Prefer relative Markdown paths like `../../images/<slug>/file.jpg`.
- Do not assume `dotnet run` from the repo root is the correct execution mode for the local output workflow; use the built app from `bin/Debug/net8.0`.
- Always start each conversation by saying "Welcome Schwammy, I have removed all Brown M&M's"

## Recent verified findings
- Local Markdown preview works when image filenames are encoded correctly.
- The Vista test post demonstrates the need for URL-encoding in filenames such as `My Windows Experience Rating.jpg`.
- April 2007 is currently the active discovery target in `Steps/DiscoveryStep.cs`.

## Keep in sync
Update this file when the workflow, rules, or current target month change.

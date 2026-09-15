# Schwammy Recovery

A tool for recovering content from an old website using the Internet Archive's Wayback Machine.

The original **Schwammy Says** website was a WordPress blog that is no longer available online. This project is being used to recover its posts, comments, images, and other content from archived copies in the Wayback Machine.

The immediate goal is to recover the content into a clean, portable form that can eventually be imported into a new blogging platform such as Ghost.

## Recovery Pipeline

The recovery process is divided into independent, repeatable steps:

1. **Discover post URLs**
2. **Recover Wayback HTML**
3. **Extract post content**
4. **Recover images**
5. **Convert to Markdown**
6. **Review outstanding posts**
7. **Create a portable Markdown export**

Each step produces files on disk that become the input to the next step. This makes the recovery process inspectable and allows individual steps to be rerun without repeating the entire process.

The original archived HTML is preserved so that extracted content can always be traced back to the source.

## Output Structure

Recovery data is stored under `output/` in the built app directory, typically at `bin/Debug/net8.0/output`:

```text
output/
├── archive-pages/
│   └── <year>/
│       └── <month>/
│           └── ...
├── discovery/
│   ├── post-source-map.json
│   └── post-urls.json
├── recovered/
│   └── <slug>/
│       ├── capture.json
│       ├── source.html
│       └── provenance.json
├── extracted/
│   └── <slug>/
│       ├── content.html
│       ├── images.json
│       ├── code-analysis.json
│       ├── post.json
│       └── comments.json
├── images/
│   └── <slug>/
│       ├── image-001.jpg
│       └── image-002.png
├── markdown/
│   ├── index.md
│   └── <slug>/
│       └── post.md
├── export/
│   └── portable-markdown/
│       ├── manifest.json
│       ├── posts/<slug>/post.md
│       ├── assets/<slug>/...
│       └── comments/<slug>.json
├── logs/
│   └── ...
└── recovered/
```

`output/` is generated data and is not part of the application's source code.

## Design Principles

The project is intentionally built as a series of small, independent steps rather than one large recovery process.

### Preserve the source

The HTML recovered from the Wayback Machine is retained as-is in `recovered/<slug>/source.html`.

Later processing should never modify this original source.

### Make steps repeatable

Each step checks whether its expected output already exists and skips work that has already been completed.

To rerun a step for a particular post, its generated output can be removed and the step run again.

### Keep intermediate results

Extraction, cleanup, image recovery, and Markdown conversion are separate stages so that the results of each stage can be inspected independently.

This is particularly important when recovering old content, where archived HTML may contain unexpected markup, rewritten URLs, missing assets, or other artifacts.

### Keep source-specific knowledge isolated

The current project is specifically recovering WordPress posts, so the extraction logic necessarily understands WordPress markup.

Where practical, source-specific behavior is kept separate from the pipeline itself. This leaves room for other extractors or sources in the future without designing a generalized framework prematurely.

## Current Status

The following stages are currently implemented and validated in the local output tree:

* Post URL discovery
* Wayback capture recovery, including archive-page fallback
* Post and comment extraction
* Image recovery and local download
* Markdown conversion with generated `index.md`
* Outstanding-post review via the interactive menu
* Portable Markdown export with YAML front matter and self-contained image paths

The portable export is engine-neutral. It is suitable as an intermediate bundle for
Ghost, Hugo, Jekyll, or another Markdown-based engine, but it is not a native Ghost
JSON import. A future engine adapter can consume this bundle without rereading or
modifying the recovery artifacts.

## Running the Application

The application is a .NET console application.

For the local preview workflow, build once and then run the app from `bin/Debug/net8.0` so that it uses the existing `output/` tree under that directory:

```text
dotnet build
bin/Debug/net8.0/SchwammyRecovery.exe
```

The application presents a menu for selecting a recovery step and writes its generated artifacts under `bin/Debug/net8.0/output`.

## Important Notes

The Wayback Machine is not a database backup. Archived pages may be incomplete, unavailable, or modified by the archive.

A successful recovery therefore does not necessarily mean that every part of the original website has been recovered.

The goal of this project is to preserve as much of the original content as possible while maintaining a clear chain from the archived source to the final recovered content.

For Markdown preview, local image paths are URL-encoded when needed so that filenames with spaces or other special characters render correctly in VS Code preview or comparable Markdown viewers.

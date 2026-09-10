# Schwammy Recovery

A tool for recovering content from an old website using the Internet Archive's Wayback Machine.

The original **Schwammy Says** website was a WordPress blog that is no longer available online. This project is being used to recover its posts, comments, images, and other content from archived copies in the Wayback Machine.

The immediate goal is to recover the content into a clean, portable form that can eventually be imported into a new blogging platform such as Ghost.

## Recovery Pipeline

The recovery process is divided into independent, repeatable steps:

1. **Discover post URLs**
2. **Recover Wayback HTML**
3. **Extract post content**
4. **Clean / normalize extracted content**
5. **Recover images**
6. **Convert to Markdown**
7. **Review recovered posts**
8. **Export to Ghost**

Each step produces files on disk that become the input to the next step. This makes the recovery process inspectable and allows individual steps to be rerun without repeating the entire process.

The original archived HTML is preserved so that extracted and cleaned content can always be traced back to the source.

## Output Structure

Recovery data is stored under `output/`:

```text
output/
├── discovery/
│   └── post-urls.json
├── recovered/
│   └── <slug>/
│       ├── capture.json
│       └── source.html
├── extracted/
│   └── <slug>/
│       ├── post.json
│       ├── content.html
│       └── comments.json
├── cleaned/
│   └── <slug>/
│       ├── post.json
│       ├── content.html
│       └── comments.json
├── images/
│   └── <slug>/
│       ├── image-001.jpg
│       └── image-002.png
└── markdown/
    └── <slug>/
        └── post.md
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

The following stages are currently implemented:

* Post URL discovery
* Wayback capture recovery
* Post and comment extraction

The remaining stages will be implemented incrementally as the recovered content is inspected and additional requirements become clear.

## Running the Application

The application is a .NET console application.

Run it from the project directory:

```text
dotnet run
```

The application presents a menu for selecting a recovery step.

## Important Notes

The Wayback Machine is not a database backup. Archived pages may be incomplete, unavailable, or modified by the archive.

A successful recovery therefore does not necessarily mean that every part of the original website has been recovered.

The goal of this project is to preserve as much of the original content as possible while maintaining a clear chain from the archived source to the final recovered content.

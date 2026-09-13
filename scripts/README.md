# Analysis Scripts

This folder contains small helper scripts for repeated analysis of the generated output tree.

## `Analyze-Output.ps1`

Runs a lightweight end-to-end output audit for the current built app output directory.

Usage:

```powershell
cd scripts
./Analyze-Output.ps1
```

Optional override:

```powershell
./Analyze-Output.ps1 -OutputRoot 'C:\path\to\custom\output'
```

What it checks:

- discovered vs recovered post coverage
- missing recovered/extracted/image/Markdown artifacts
- presence of `output/markdown/index.md`
- overall artifact consistency for the discovered post list

This is meant to make repeated validation easier after recovery logic or output-format changes.

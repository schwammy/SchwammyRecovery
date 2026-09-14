param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0\output')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $OutputRoot)) {
    throw "Output directory not found: $OutputRoot"
}

$resolvedOutputRoot = (Resolve-Path $OutputRoot).Path

function Get-Slug {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return ''
    }

    try {
        $uri = [System.Uri]$Url
    }
    catch {
        return ''
    }

    $path = $uri.AbsolutePath.Trim('/')
    if ([string]::IsNullOrWhiteSpace($path)) {
        return ''
    }

    $segments = $path.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    return $segments[-1]
}

function Is-LocalBlogImageUrl {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $false
    }

    try {
        $uri = [System.Uri]$Url
    }
    catch {
        return $false
    }

    $hostName = $uri.Host

    return $hostName.Equals('schwammysays.net', [System.StringComparison]::OrdinalIgnoreCase) -or
    $hostName.Equals('www.schwammysays.net', [System.StringComparison]::OrdinalIgnoreCase) -or
    $hostName.EndsWith('.schwammysays.net', [System.StringComparison]::OrdinalIgnoreCase)
}

Write-Host "Analyzing output from $resolvedOutputRoot"
Write-Host ''

$postUrlsPath = Join-Path $resolvedOutputRoot 'discovery/post-urls.json'
$discoveredPosts = @((Get-Content -Raw -Path $postUrlsPath | ConvertFrom-Json))

$recoveredRoot = Join-Path $resolvedOutputRoot 'recovered'
$recoveredSlugs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if (Test-Path $recoveredRoot) {
    foreach ($directory in Get-ChildItem -Path $recoveredRoot -Directory) {
        $slug = $directory.Name
        $sourceExists = Test-Path (Join-Path $directory.FullName 'source.html')
        $captureExists = Test-Path (Join-Path $directory.FullName 'capture.json')

        if ($sourceExists -and $captureExists) {
            [void]$recoveredSlugs.Add($slug)
        }
    }
}

$outstandingPosts = New-Object System.Collections.Generic.List[string]
foreach ($postUrl in $discoveredPosts) {
    $slug = Get-Slug $postUrl
    if (-not [string]::IsNullOrWhiteSpace($slug) -and -not $recoveredSlugs.Contains($slug)) {
        $outstandingPosts.Add($postUrl)
    }
}

Write-Host "discovered_count=$($discoveredPosts.Count)"
Write-Host "recovered_valid_count=$($recoveredSlugs.Count)"
Write-Host "outstanding_count=$($outstandingPosts.Count)"

if ($outstandingPosts.Count -gt 0) {
    Write-Host ''
    Write-Host 'Outstanding posts:'
    foreach ($postUrl in $outstandingPosts) {
        Write-Host "  - $postUrl"
    }
}

Write-Host ''

$issues = New-Object System.Collections.Generic.List[string]
$checkedPosts = 0
$missingImagePosts = @{}
$missingImageFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$missingLocalImageFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$missingExternalImageFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($postUrl in $discoveredPosts) {
    $slug = Get-Slug $postUrl
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $issues.Add("Unable to derive slug from $postUrl")
        continue
    }

    $checkedPosts++

    $recoveredDirectory = Join-Path $resolvedOutputRoot "recovered/$slug"
    $extractedDirectory = Join-Path $resolvedOutputRoot "extracted/$slug"
    $imagesDirectory = Join-Path $resolvedOutputRoot "images/$slug"
    $markdownFile = Join-Path $resolvedOutputRoot "markdown/$slug/post.md"

    foreach ($artifact in @('source.html', 'capture.json')) {
        $artifactPath = Join-Path $recoveredDirectory $artifact
        if (-not (Test-Path $artifactPath)) {
            $issues.Add(("Missing recovered artifact for {0}: {1}" -f $slug, $artifact))
        }
    }

    foreach ($artifact in @('content.html', 'images.json', 'post.json')) {
        $artifactPath = Join-Path $extractedDirectory $artifact
        if (-not (Test-Path $artifactPath)) {
            $issues.Add(("Missing extracted artifact for {0}: {1}" -f $slug, $artifact))
        }
    }

    if (-not (Test-Path $imagesDirectory)) {
        $issues.Add("Missing image directory for $slug")
    }

    if (-not (Test-Path $markdownFile)) {
        $issues.Add("Missing Markdown file for $slug")
    }

    $imagesJsonPath = Join-Path $resolvedOutputRoot "extracted/$slug/images.json"
    if (Test-Path $imagesJsonPath) {
        $images = @((Get-Content -Raw -Path $imagesJsonPath | ConvertFrom-Json))

        $localMissing = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $externalMissing = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        foreach ($image in $images) {
            if ($null -eq $image) {
                continue
            }

            $attempts = New-Object System.Collections.Generic.List[pscustomobject]

            if (-not [string]::IsNullOrWhiteSpace($image.LinkedImageUrl) -and -not [string]::IsNullOrWhiteSpace($image.LinkedImageFileName)) {
                $attempts.Add([pscustomobject]@{
                        FileName = $image.LinkedImageFileName
                        Url      = $image.LinkedImageUrl
                    })
            }

            if (-not [string]::IsNullOrWhiteSpace($image.SourceUrl) -and -not [string]::IsNullOrWhiteSpace($image.FileName)) {
                $attempts.Add([pscustomobject]@{
                        FileName = $image.FileName
                        Url      = $image.SourceUrl
                    })
            }

            if ($attempts.Count -eq 0) {
                continue
            }

            $anyDownloaded = $false
            foreach ($attempt in $attempts) {
                $imagePath = Join-Path $imagesDirectory $attempt.FileName
                if (Test-Path $imagePath) {
                    $anyDownloaded = $true
                    break
                }
            }

            if ($anyDownloaded) {
                continue
            }

            foreach ($attempt in $attempts) {
                [void]$missingImageFiles.Add($attempt.FileName)

                if (Is-LocalBlogImageUrl $attempt.Url) {
                    [void]$localMissing.Add($attempt.FileName)
                    [void]$missingLocalImageFiles.Add($attempt.FileName)
                }
                else {
                    [void]$externalMissing.Add($attempt.FileName)
                    [void]$missingExternalImageFiles.Add($attempt.FileName)
                }
            }
        }

        if ($localMissing.Count -gt 0 -or $externalMissing.Count -gt 0) {
            $missingImagePosts[$slug] = [ordered]@{
                LocalImages    = @($localMissing | Sort-Object)
                ExternalImages = @($externalMissing | Sort-Object)
            }
        }
    }
}

$markdownRoot = Join-Path $resolvedOutputRoot 'markdown'
$markdownFiles = @()
if (Test-Path $markdownRoot) {
    $markdownFiles = @(Get-ChildItem -Path $markdownRoot -Recurse -File)
}

$indexExists = Test-Path (Join-Path $markdownRoot 'index.md')
Write-Host "markdown_file_count=$($markdownFiles.Count)"
Write-Host "markdown_index_exists=$indexExists"

Write-Host "missing_image_post_count=$($missingImagePosts.Count)"
Write-Host "missing_local_image_file_count=$($missingLocalImageFiles.Count)"
Write-Host "missing_external_image_file_count=$($missingExternalImageFiles.Count)"

if ($indexExists -eq $false) {
    $issues.Add('Missing markdown/index.md')
}

if ($missingImagePosts.Count -gt 0) {
    Write-Host ''
    Write-Host 'Missing image analysis:'

    foreach ($entry in $missingImagePosts.GetEnumerator()) {
        $slug = $entry.Key
        $localImages = @($entry.Value.LocalImages)
        $externalImages = @($entry.Value.ExternalImages)

        Write-Host "  - $slug"

        if ($localImages.Count -gt 0) {
            Write-Host "      Local blog images: $($localImages -join ', ')"
        }

        if ($externalImages.Count -gt 0) {
            Write-Host "      External images: $($externalImages -join ', ')"
        }
    }
}

Write-Host ''

if ($issues.Count -gt 0) {
    Write-Host 'Artifact consistency check: FAIL'
    foreach ($issue in $issues) {
        Write-Host "  - $issue"
    }
    exit 1
}

Write-Host 'Artifact consistency check: PASS'
Write-Host "Checked posts: $checkedPosts"

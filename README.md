# SchwammyRecovery

Small .NET 8 console app for recovering the old Schwammy Says WordPress blog
from Internet Archive/Wayback Machine monthly archive pages.

## First test

Install .NET 8 SDK, then:

```bash
dotnet restore
dotnet run -- "https://web.archive.org/web/20220925020544/http://www.schwammysays.net/2007/03/"
```

The program will crawl the supplied monthly archive and its `/page/2/`,
`/page/3/`, etc. pagination, then write:

```text
output/post-urls.json
```

The first version deliberately does NOT download every post yet. We want to
verify that URL discovery is correct before adding the recovery/extraction
stage.

## Expected result

For March 2007, the console should show lines like:

```text
POST: https://www.schwammysays.net/have-you-checked-out-resharper/
```

and other original Schwammy Says post URLs.

## Important

This program stops if Wayback returns HTTP 429 rather than retrying aggressively.
Please keep the request rate low. Once discovery is proven, we'll add polite
delays, capture selection, HTML extraction, and image recovery.

## Next stages

1. Verify March 2007 URL discovery.
2. Crawl all monthly archives from March 2007 onward.
3. Find usable Wayback captures for each post.
4. Save raw HTML locally.
5. Extract title/date/author/content/categories/tags.
6. Recover images/media where possible.
7. Generate a Ghost import file.

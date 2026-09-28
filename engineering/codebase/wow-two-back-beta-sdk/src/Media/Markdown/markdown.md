# Media.Markdown

Markdown for display — help centers, changelogs, blog posts — over Markdig (BSD-2-Clause), safe for user content.

```csharp
builder.Services.AddMarkdown();                                    // Media:Markdown in host configuration

var page = markdown.Render(source);
// page.Html · page.Text · page.Headings (level, text, anchor id) · page.FrontMatter · page.WordCount · page.ReadingMinutes
```

- GitHub-flavoured: tables, task lists, footnotes, auto-links, heading anchors (`## PDF tools` → `id="pdf-tools"`).
- Raw HTML is escaped unless `AllowRawHtml` is on — keep it off for anything a user wrote.
- Link and image URLs outside http, https, mailto, tel, relative paths and `data:image/` become `#`; Markdig alone
  renders `javascript:` links as written.
- External http(s) links get `rel="nofollow noopener noreferrer"` and `target="_blank"` (`NoFollowExternalLinks`,
  `ExternalLinksNewTab`).
- Front matter: top-level `key: value` pairs; `[a, b]` lists read as `a, b`; nested YAML is not read.
- `ToPlainText` feeds search indexes and previews; reading time uses `WordsPerMinute` (200).

---
name: post-dashboard
description: "A local ASP.NET Core web dashboard for browsing, editing, and previewing markdown-based social media posts with media assets."
user-invokable: false
metadata:
  author: Ronald Schlenker
  version: "1.0.0"
---

# Post Dashboard

A minimal, local web dashboard for managing social media posts stored as markdown files with YAML frontmatter. No database, no CMS, no cloud — just files on disk.

## How It Works

- **Tech**: ASP.NET Core (.NET 10) Minimal API + vanilla JS SPA
- **Port**: `http://localhost:5124`
- **Start**: `dotnet run`

You point it at one or more content root directories (configured in `Program.cs`). It recursively scans for `.md` files with YAML frontmatter and displays them in a dark-themed web dashboard.

## Post File Format

Every post is a `.md` file with YAML frontmatter. Files without a `---` header are ignored.

```markdown
---
status: draft
channel: LinkedIn
target: "Personal Profile"
language: en
created: 2026-02-22
posted:
notes: "Internal notes — shown in dashboard, not part of the post"
---

The actual post content goes here.

#hashtag1 #hashtag2
```

### YAML Fields

| Field | Required | Values / Description |
|-------|----------|---------------------|
| `status` | yes | `draft` or `posted` |
| `channel` | yes | `LinkedIn`, `X`, `Bluesky`, `TikTok`, `Instagram`, `Reddit`, `Discord` |
| `created` | yes | `YYYY-MM-DD` |
| `target` | no | Account/profile (e.g. `"@handle"`, `"Personal Profile"`) |
| `language` | no | e.g. `en`, `de` |
| `posted` | no | Date when published (leave empty for drafts) |
| `notes` | no | Internal notes, not part of the post body |

If no `channel` field is present, the channel is detected from the filename (e.g. `LinkedIn.md` -> LinkedIn).

## Organizing Posts

The recommended structure is one folder per post idea, one markdown file per platform:

```
my-post-idea/
├── LinkedIn.md
├── X.md
├── Bluesky.md
├── photo.jpg        # Media assets live alongside posts
└── demo.mp4
```

### Media Assets

Images and videos placed in the same folder as the post files are automatically picked up and shown as thumbnails.

Supported formats: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.mp4`, `.mov`

## Dashboard Features

- **Tree sidebar**: Navigates all configured content roots; only shows folders that contain YAML posts
- **Card view**: Preview or expanded mode for all posts in a folder
- **Detail view**: Full post content with inline editing and save
- **Media lightbox**: Click any asset thumbnail to see a large preview (images) or play video (with streaming/seeking)
- **Copy**: Copy post text or folder path to clipboard
- **Drag & drop**: Drag media assets into other apps

## Configuration

Content roots are configured in `Program.cs`:

```csharp
var contentRoots = new (string Name, string Path)[]
{
    ("My Posts", "/path/to/my/posts"),
    ("Another Folder", "/path/to/other/content"),
};
```

## API

| Endpoint | Description |
|----------|-------------|
| `GET /api/roots` | Tree structure of all content roots |
| `GET /api/folder?path=` | Posts and media in a folder |
| `GET /api/post?path=` | Full post content with parsed frontmatter |
| `POST /api/post/save` | Save edited post body (preserves frontmatter) |
| `GET /media?path=` | Serve media files (supports HTTP Range Requests) |

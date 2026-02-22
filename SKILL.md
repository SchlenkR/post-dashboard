---
name: post-dashboard
description: "Manage social media posts via the Post Dashboard — a local ASP.NET Core web tool for browsing, editing, and previewing markdown-based posts with media assets."
user-invokable: false
metadata:
  author: Ronald
  version: "1.0.0"
---

# Post Dashboard Skill

Local web dashboard for managing social media posts stored as markdown files with YAML frontmatter.

## Quick Reference

- **Repo**: `~/repos/github/post-dashboard/`
- **Symlink**: `ronaldsWorkspace/tools/post-dashboard` -> repo
- **Tech**: ASP.NET Core (.NET 10) Minimal API, vanilla JS SPA
- **Port**: `http://localhost:5124`
- **Start**: `dotnet run --project tools/post-dashboard`
- **VSCode Task**: `WS :: post-dashboard: start`

## Post File Format

Every post is a `.md` file with YAML frontmatter. Files without `---` header are ignored.

```markdown
---
status: draft
channel: LinkedIn
target: "Personal Profile"
language: en
created: 2026-02-22
posted:
notes: "Optional description of the post's purpose"
---

The actual post content goes here.

#hashtag1 #hashtag2
```

### Required YAML Fields

| Field | Values | Description |
|-------|--------|-------------|
| `status` | `draft`, `posted` | Current state of the post |
| `channel` | `LinkedIn`, `X`, `Bluesky`, `TikTok`, `Instagram`, `Reddit`, `Discord` | Target platform |
| `created` | `YYYY-MM-DD` | Creation date |

### Optional YAML Fields

| Field | Description |
|-------|-------------|
| `target` | Account/profile (e.g. `"@SchlenkR"`, `"Personal Profile"`) |
| `language` | `en` or `de` |
| `posted` | Date when posted (leave empty for drafts) |
| `notes` | Internal notes (shown in dashboard, not part of post) |

## Folder Structure

Posts live in the Obsidian vault under `SocialMedia/`:

```
SocialMedia/
├── _ideas_general/     # Tech content — one subfolder per post idea
│   └── Post Dashboard Tool/
│       ├── LinkedIn.md
│       ├── X.md
│       ├── Bluesky.md
│       └── screenshot.png    # Assets live alongside posts
├── _ideas_pxl/         # PXL Clock marketing posts
├── _assets/            # Shared media assets
└── _done/              # Archived/completed posts
```

### Conventions

- One **folder per post idea**, one **file per platform**
- File naming: `{Channel}.md` (e.g. `LinkedIn.md`, `X.md`, `Bluesky.md`)
- **Assets** (images, videos) go directly in the post folder
- Supported media: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.mp4`, `.mov`
- Channel detection: from YAML `channel` field, or from filename prefix

## Dashboard Features

- **Sidebar**: Tree navigation of all post folders (only shows folders containing YAML posts)
- **Card View**: Preview or expanded mode for all posts in a folder
- **Detail View**: Click a post to see full content, edit inline, and save
- **Media Preview**: Thumbnails with filenames; click to open lightbox (images + video streaming)
- **Copy**: Copy post text or folder path to clipboard
- **Drag & Drop**: Drag media assets to other apps
- **Video Streaming**: HTTP Range Request support for seeking/scrubbing

## Content Roots

The dashboard scans these directories (configured in `Program.cs`):

```csharp
("SocialMedia", "...Default/SocialMedia"),
("VideoProduction", "...Default/VideoProduction"),
```

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/roots` | Tree structure of all content roots |
| `GET /api/folder?path=` | Posts and media in a folder |
| `GET /api/post?path=` | Full post content with frontmatter |
| `POST /api/post/save` | Save edited post body (preserves frontmatter) |
| `GET /media?path=` | Serve media files (with Range Request support) |
| `GET /health` | Server status |

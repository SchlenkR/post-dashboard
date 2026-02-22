using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5124");

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// ============================================================
// Configuration
// ============================================================

// Content roots can be configured here or passed via command line:
//   dotnet run -- "My Posts=/path/to/posts" "Other=/path/to/content"
var contentRoots = args.Length > 0
    ? args.Select(a =>
    {
        var eq = a.IndexOf('=');
        return eq > 0
            ? (Name: a[..eq], Path: a[(eq + 1)..])
            : (Name: Path.GetFileName(a), Path: a);
    }).ToArray()
    : new (string Name, string Path)[]
    {
        // If no args provided, add your content directories here:
        // ("My Posts", "/path/to/your/posts"),
    };

var excludeDirs = new HashSet<string>(StringComparer.Ordinal)
{
    "node_modules", ".git", ".obsidian", ".trash",
    "remotion-project", "bin", "obj", ".vs", ".next",
    "__pycache__", ".venv", "dist", "build",
};

var mediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov",
};

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

// ============================================================
// Helpers
// ============================================================

bool IsAllowedPath(string path) =>
    contentRoots.Any(r => path.StartsWith(r.Path, StringComparison.Ordinal));

List<string> GetSubFolders(string dir)
{
    try
    {
        return [.. Directory.GetDirectories(dir)
            .Where(d => !excludeDirs.Contains(Path.GetFileName(d)))
            .Order()];
    }
    catch { return []; }
}

List<string> GetMdFiles(string dir)
{
    try { return [.. Directory.GetFiles(dir, "*.md").Order()]; }
    catch { return []; }
}

List<string> GetMediaFiles(string dir)
{
    try
    {
        return [.. Directory.GetFiles(dir)
            .Where(f => mediaExtensions.Contains(Path.GetExtension(f)))
            .Order()];
    }
    catch { return []; }
}

bool HasYamlFrontMatter(string filePath)
{
    try
    {
        var content = File.ReadAllText(filePath).TrimStart();
        if (!content.StartsWith("---")) return false;
        var nl = content.IndexOf('\n');
        return nl >= 0 && content[(nl + 1)..].Contains("\n---");
    }
    catch { return false; }
}

(Dictionary<string, string> FrontMatter, string Body) ParseFrontMatter(string content)
{
    var trimmed = content.TrimStart();
    if (!trimmed.StartsWith("---"))
        return ([], content);

    var nl = trimmed.IndexOf('\n');
    if (nl < 0) return ([], content);

    var rest = trimmed[(nl + 1)..];
    var end = rest.IndexOf("\n---", StringComparison.Ordinal);
    if (end < 0) return ([], content);

    var yaml = rest[..end];
    var body = rest[(end + 4)..].TrimStart();

    var pairs = new Dictionary<string, string>();
    foreach (var line in yaml.Split('\n'))
    {
        var colon = line.IndexOf(':');
        if (colon <= 0) continue;
        var key = line[..colon].Trim();
        var val = line[(colon + 1)..].Trim().Trim('"');
        if (key.Length > 0) pairs[key] = val;
    }
    return (pairs, body);
}

object? BuildTree(string dir)
{
    var hasPosts = GetMdFiles(dir).Exists(HasYamlFrontMatter);
    var children = GetSubFolders(dir)
        .Select(BuildTree)
        .Where(c => c is not null)
        .ToList();

    if (!hasPosts && children.Count == 0)
        return null;

    return new
    {
        Name = Path.GetFileName(dir),
        Path = dir,
        HasPosts = hasPosts,
        Children = children,
    };
}

object? ParsePost(string filePath)
{
    try
    {
        var content = File.ReadAllText(filePath);
        var (fm, body) = ParseFrontMatter(content);
        if (fm.Count == 0) return null;

        var clean = body.TrimStart('#', ' ', '\n', '\r');
        var preview = clean.Length > 250 ? clean[..250] + "..." : clean;

        return new
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            FrontMatter = fm,
            BodyPreview = preview,
            MediaFiles = GetMediaFiles(Path.GetDirectoryName(filePath)!),
        };
    }
    catch { return null; }
}

string GetMimeType(string ext) => ext.ToLowerInvariant() switch
{
    ".jpg" or ".jpeg" => "image/jpeg",
    ".png" => "image/png",
    ".gif" => "image/gif",
    ".webp" => "image/webp",
    ".mp4" => "video/mp4",
    ".mov" => "video/quicktime",
    _ => "application/octet-stream",
};

// ============================================================
// API Routes
// ============================================================

app.MapGet("/api/roots", () =>
{
    var roots = contentRoots.Select(r => new
    {
        r.Name,
        r.Path,
        HasPosts = GetMdFiles(r.Path).Exists(HasYamlFrontMatter),
        Children = GetSubFolders(r.Path)
            .Select(BuildTree)
            .Where(c => c is not null)
            .ToList(),
    });
    return Results.Json(roots, jsonOptions);
});

app.MapGet("/api/folder", (string path) =>
{
    if (!IsAllowedPath(path) || !Directory.Exists(path))
        return Results.NotFound("Folder not found");

    return Results.Json(new
    {
        Name = Path.GetFileName(path),
        Path = path,
        Posts = GetMdFiles(path).Select(ParsePost).Where(p => p is not null),
        MediaFiles = GetMediaFiles(path),
    }, jsonOptions);
});

app.MapGet("/api/post", (string path) =>
{
    if (!IsAllowedPath(path) || !File.Exists(path))
        return Results.NotFound("Post not found");

    var (fm, body) = ParseFrontMatter(File.ReadAllText(path));
    return Results.Json(new
    {
        FileName = Path.GetFileName(path),
        FilePath = path,
        FrontMatter = fm,
        FullBody = body,
        MediaFiles = GetMediaFiles(Path.GetDirectoryName(path)!),
    }, jsonOptions);
});

app.MapPost("/api/post/save", async (HttpRequest req) =>
{
    var doc = await JsonDocument.ParseAsync(req.Body);
    var path = doc.RootElement.GetProperty("path").GetString()!;
    var newBody = doc.RootElement.GetProperty("body").GetString()!;

    if (!IsAllowedPath(path) || !File.Exists(path))
        return Results.NotFound("Post not found");

    var content = File.ReadAllText(path);
    var trimmed = content.TrimStart();
    if (!trimmed.StartsWith("---"))
        return Results.BadRequest("No frontmatter found");

    var nl = trimmed.IndexOf('\n');
    if (nl < 0) return Results.BadRequest("Invalid frontmatter");
    var rest = trimmed[(nl + 1)..];
    var end = rest.IndexOf("\n---", StringComparison.Ordinal);
    if (end < 0) return Results.BadRequest("Invalid frontmatter");

    var frontmatterBlock = trimmed[..(nl + 1 + end + 4)];
    var updated = frontmatterBlock + "\n" + newBody;
    File.WriteAllText(path, updated);

    return Results.Ok(new { saved = true });
});

app.MapGet("/media", async (string path, HttpContext ctx) =>
{
    if (!IsAllowedPath(path) || !File.Exists(path))
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    var fileInfo = new FileInfo(path);
    var contentType = GetMimeType(fileInfo.Extension);
    var fileLength = fileInfo.Length;

    ctx.Response.Headers.CacheControl = "max-age=3600";
    ctx.Response.Headers.AcceptRanges = "bytes";

    var rangeHeader = ctx.Request.Headers.Range.FirstOrDefault();
    if (rangeHeader is not null && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
    {
        // Parse range: "bytes=start-end" or "bytes=start-"
        var range = rangeHeader["bytes=".Length..];
        var parts = range.Split('-', 2);
        var start = long.TryParse(parts[0], out var s) ? s : 0L;
        var end = parts.Length > 1 && long.TryParse(parts[1], out var e) ? e : fileLength - 1;

        if (start >= fileLength || end >= fileLength || start > end)
        {
            ctx.Response.StatusCode = 416; // Range Not Satisfiable
            ctx.Response.Headers.ContentRange = $"bytes */{fileLength}";
            return;
        }

        var chunkSize = end - start + 1;
        ctx.Response.StatusCode = 206;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength = chunkSize;
        ctx.Response.Headers.ContentRange = $"bytes {start}-{end}/{fileLength}";

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(start, SeekOrigin.Begin);

        var buffer = new byte[64 * 1024];
        var remaining = chunkSize;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await fs.ReadAsync(buffer.AsMemory(0, toRead), ctx.RequestAborted);
            if (read == 0) break;
            await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, read), ctx.RequestAborted);
            remaining -= read;
        }
    }
    else
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength = fileLength;
        await ctx.Response.SendFileAsync(path, ctx.RequestAborted);
    }
});

app.MapGet("/health", () => new { Status = "ok", Port = 5124 });

Console.WriteLine("Post Dashboard running on http://localhost:5124/");
app.Run();

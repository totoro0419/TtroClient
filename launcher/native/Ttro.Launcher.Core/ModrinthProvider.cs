using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;

namespace Ttro.Launcher.Core;

public sealed class ModrinthProvider : IContentProvider
{
    public string Id => "modrinth";
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Time, string Json)> cache = [];
    private DateTimeOffset nextRequest;
    public ModrinthProvider(HttpClient? client = null) => http = client ?? ContentService.CreateClient();
    private async Task<JsonNode> GetAsync(string path, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(path, out var cached) && DateTimeOffset.UtcNow - cached.Time < TimeSpan.FromMinutes(2)) return JsonNode.Parse(cached.Json)!;
            var wait = nextRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            using var response = await http.GetAsync("https://api.modrinth.com/v2/" + path, HttpCompletionOption.ResponseHeadersRead, ct);
            nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(250);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var seconds = response.Headers.TryGetValues("X-Ratelimit-Reset", out var v) && int.TryParse(v.FirstOrDefault(), out var s) ? Math.Clamp(s, 1, 300) : 60;
                nextRequest = DateTimeOffset.UtcNow.AddSeconds(seconds);
                throw new HttpRequestException($"Modrinth request limit reached. Retry in {seconds} seconds.");
            }
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream(); var block = new byte[65536]; int n;
            while ((n = await stream.ReadAsync(block, ct)) > 0) { if (buffer.Length + n > 8 * 1024 * 1024) throw new InvalidDataException("Provider response exceeds the metadata limit."); buffer.Write(block, 0, n); }
            var json = System.Text.Encoding.UTF8.GetString(buffer.ToArray()); var result = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty provider response.");
            if (cache.Count >= 32) cache.Remove(cache.MinBy(x => x.Value.Time).Key);
            cache[path] = (DateTimeOffset.UtcNow, json); return result;
        }
        finally { gate.Release(); }
    }
    private static string[] Strings(JsonNode? node) => node is JsonArray a ? a.Select(n => n!.GetValue<string>()).ToArray() : [];
    private static DateTimeOffset? Date(JsonNode? node) => DateTimeOffset.TryParse(node?.GetValue<string>(), out var date) ? date : null;
    private static SearchHit Hit(JsonNode n, string kind, bool details = false) => new(n[details ? "id" : "project_id"]!.GetValue<string>(), n["title"]!.GetValue<string>(), n["description"]!.GetValue<string>(), kind)
    {
        Author = n["author"]?.GetValue<string>(), IconUrl = n["icon_url"]?.GetValue<string>(), ThumbnailUrl = n["featured_gallery"]?.GetValue<string>(),
        Gallery = details ? n["gallery"]?.AsArray().OrderByDescending(g => g!["featured"]?.GetValue<bool>() == true).Select(g => g!["url"]!.GetValue<string>()).Take(12).ToArray() ?? [] : Strings(n["gallery"]),
        Categories = Strings(n["categories"]), Downloads = n["downloads"]?.GetValue<long>(), Updated = Date(n[details ? "updated" : "date_modified"]),
        License = details ? n["license"]?["id"]?.GetValue<string>() : n["license"]?.GetValue<string>()
    };
    public async Task<ContentPage> SearchAsync(ContentQuery query, CancellationToken ct)
    {
        ContentService.ValidateKind(query.Kind);
        if (!new[] { "relevance", "downloads", "updated", "newest", "follows" }.Contains(query.Sort) || query.Offset < 0 || query.Limit is < 1 or > 24) throw new ArgumentException("Unsupported search options.");
        var facets = new List<string[]> { new[] { "versions:1.8.9" }, new[] { "project_type:" + query.Kind } };
        if (query.Kind == "mod") facets.Add(["categories:forge"]);
        // Only provider category tags; never infer tags/resolution from a title.
        foreach (var tag in new[] { query.Category, query.Resolution }.Where(t => !string.IsNullOrEmpty(t)))
        { if (!tag!.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '+')) throw new ArgumentException("Invalid category tag."); facets.Add(["categories:" + tag]); }
        var path = "search?limit=" + query.Limit + "&offset=" + query.Offset + "&index=" + query.Sort + "&query=" + Uri.EscapeDataString(query.Query) + "&facets=" + Uri.EscapeDataString(JsonSerializer.Serialize(facets));
        var root = await GetAsync(path, ct);
        return new(root["hits"]!.AsArray().Where(n => Strings(n!["versions"]).Contains("1.8.9") && n["project_type"]!.GetValue<string>() == query.Kind && (query.Kind != "mod" || Strings(n["categories"]).Contains("forge"))).Select(n => Hit(n!, query.Kind)).ToArray(), query.Offset, root["total_hits"]!.GetValue<int>());
    }
    public async Task<ContentDetails> DetailsAsync(string projectId, string kind, CancellationToken ct)
    {
        var n = await GetAsync("project/" + Uri.EscapeDataString(projectId), ct);
        if (n["project_type"]!.GetValue<string>() != kind) throw new InvalidDataException("Provider project type mismatch.");
        return new(Hit(n, kind, true), n["body"]?.GetValue<string>() ?? "", Strings(n["game_versions"]));
    }
    public async Task<ContentVersion[]> VersionsAsync(string projectId, string kind, CancellationToken ct)
    {
        ContentService.ValidateKind(kind);
        var n = await GetAsync("project/" + Uri.EscapeDataString(projectId) + "/version?game_versions=%5B%221.8.9%22%5D" + (kind == "mod" ? "&loaders=%5B%22forge%22%5D" : ""), ct);
        return n.AsArray().Select(v => Version(v!)).Where(v => ContentService.Compatible(v, kind)).OrderByDescending(v => v.Published).ToArray();
    }
    public async Task<ContentVersion> VersionAsync(string versionId, CancellationToken ct) => Version(await GetAsync("version/" + Uri.EscapeDataString(versionId), ct));
    private static ContentVersion Version(JsonNode n) => new(n["project_id"]!.GetValue<string>(), n["id"]!.GetValue<string>(), n["version_number"]!.GetValue<string>(), n["name"]!.GetValue<string>(), Date(n["date_published"]) ?? throw new InvalidDataException("Invalid release date."), n["version_type"]!.GetValue<string>(), Strings(n["game_versions"]), Strings(n["loaders"]),
        n["files"]!.AsArray().Select(f => new ContentFile(f!["filename"]!.GetValue<string>(), f["url"]!.GetValue<string>(), f["hashes"]?["sha512"]?.GetValue<string>() ?? "", f["size"]!.GetValue<long>(), f["primary"]?.GetValue<bool>() == true)).ToArray(),
        n["dependencies"]!.AsArray().Select(d => new ContentDependency(d!["project_id"]?.GetValue<string>(), d["version_id"]?.GetValue<string>(), d["dependency_type"]!.GetValue<string>())).ToArray());
}

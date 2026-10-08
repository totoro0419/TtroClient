using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace Ttro.Launcher.Core;

public sealed record SearchHit(string ProjectId, string Title, string Description, string Kind) { public override string ToString() => Title; }
public sealed class ContentService(ProfileStore store)
{
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("TtroClient/0.3.0-alpha.1 (https://github.com/totoro0419/TtroClient)"); return h; }
    public string DirectoryFor(string kind) => Path.Combine(store.GameDirectory(store.Current), kind == "mod" ? "mods" : "resourcepacks");
    public string[] Files(string kind) => Directory.Exists(DirectoryFor(kind)) ? Directory.GetFiles(DirectoryFor(kind)).Where(p => (p.EndsWith(".jar") || p.EndsWith(".zip") || p.EndsWith(".disabled")) && !Path.GetFileName(p).StartsWith("ttro-client-1.8.9-", StringComparison.Ordinal)).Select(p => Path.GetFileName(p)).ToArray()! : [];
    public bool HasProvider(string modid, bool includeDisabled = false)
    {
        foreach (var p in Directory.Exists(DirectoryFor("mod")) ? Directory.GetFiles(DirectoryFor("mod")).Where(p => p.EndsWith(".jar") || includeDisabled && p.EndsWith(".jar.disabled")) : [])
            try { using var z = ZipFile.OpenRead(p); var entry = z.GetEntry("mcmod.info"); if (entry is null) continue; using var reader = new StreamReader(entry.Open()); var raw = reader.ReadToEnd(); if (raw.Contains("\"" + modid + "\"", StringComparison.Ordinal)) return true; } catch (InvalidDataException) { }
        return false;
    }
    public static void ValidateArchive(string path, string kind, bool verifiedUpstreamMetadata = false)
    {
        if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new InvalidDataException("Content exceeds 256 MiB.");
        using var zip = ZipFile.OpenRead(path); long total = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            total += entry.Length;
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Split('/').Any(x => x == "..") || name.Contains(':') || !seen.Add(name)) throw new InvalidDataException("Unsafe or duplicate ZIP entry.");
            if (total > 2L * 1024 * 1024 * 1024 || zip.Entries.Count > 100000) throw new InvalidDataException("Archive expansion limit exceeded.");
        }
        if (kind == "resourcepack")
        {
            var e = zip.GetEntry("pack.mcmeta") ?? throw new InvalidDataException("pack.mcmeta is missing at the ZIP root.");
            using var r = new StreamReader(e.Open()); var meta = JsonNode.Parse(r.ReadToEnd());
            if (meta?["pack"]?["pack_format"]?.GetValue<int>() != 1) throw new InvalidDataException("This pack is not declared for Minecraft 1.8.9 (pack_format 1).");
        }
        else
        {
            if (zip.GetEntry("fabric.mod.json") is not null || zip.GetEntry("META-INF/mods.toml") is not null || zip.GetEntry("quilt.mod.json") is not null) throw new InvalidDataException("This is not a Forge 1.8.9 mod.");
            var meta = zip.GetEntry("mcmod.info");
            if (meta is null && !verifiedUpstreamMetadata) throw new InvalidDataException("A locally imported mod needs Forge 1.8.9 metadata.");
            if (meta is not null) { using var r = new StreamReader(meta.Open()); var text = r.ReadToEnd(); if (text.Contains("\"mousetweaks\"")) throw new InvalidDataException("Mouse Tweaks is already integrated into Ttro Client."); if (!verifiedUpstreamMetadata && !text.Contains("1.8.9")) throw new InvalidDataException("The mod does not declare Minecraft 1.8.9."); }
            var header = new byte[8];
            foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".class"))) { using var stream = e.Open(); if (stream.Read(header, 0, 8) == 8 && header[0] == 0xca && header[1] == 0xfe && header[2] == 0xba && header[3] == 0xbe && (header[6] * 256 + header[7]) > 52) throw new InvalidDataException("This mod requires a newer Java version than Java 8."); }
        }
    }
    public void Import(string file, string kind)
    {
        store.EnsureEditable(); var name = ProfileStore.SafeName(Path.GetFileName(file));
        if (kind == "mod" && !name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || kind == "resourcepack" && !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Select a .jar mod or .zip resource pack.");
        ValidateArchive(file, kind); Directory.CreateDirectory(DirectoryFor(kind)); var target = Path.Combine(DirectoryFor(kind), name);
        if (File.Exists(target) || File.Exists(target + ".disabled")) throw new IOException("This filename already exists. Remove or rename the existing content first.");
        File.Copy(file, target);
        if (kind == "resourcepack") { store.Current.Packs.Insert(0, name); SyncPacks(); }
        store.Save();
    }
    public void Toggle(string name, string kind)
    {
        store.EnsureEditable(); ProfileStore.SafeName(name); var path = Path.Combine(DirectoryFor(kind), name);
        if (kind == "mod") { var other = name.EndsWith(".disabled") ? path[..^9] : path + ".disabled"; if (File.Exists(other)) throw new IOException("Content destination already exists."); File.Move(path, other); }
        else { if (!File.Exists(path)) throw new FileNotFoundException(); if (!store.Current.Packs.Remove(name)) store.Current.Packs.Insert(0, name); SyncPacks(); }
        store.Save();
    }
    public void MovePack(string name, int delta)
    {
        store.EnsureEditable(); var list = store.Current.Packs; var i = list.IndexOf(name); if (i < 0) throw new InvalidOperationException("Enable this pack before reordering."); var j = Math.Clamp(i + delta, 0, list.Count - 1); (list[i], list[j]) = (list[j], list[i]); SyncPacks(); store.Save();
    }
    public void Delete(string name, string kind)
    {
        store.EnsureEditable(); ProfileStore.SafeName(name); var path = Path.Combine(DirectoryFor(kind), name); store.Backup(path); File.Delete(path); store.Current.Packs.Remove(name); store.Current.ManagedMods.Remove(name.Replace(".disabled", "")); if (kind == "resourcepack") SyncPacks(); store.Save();
    }
    public void SyncPacks()
    {
        var path = Path.Combine(store.GameDirectory(store.Current), "options.txt");
        var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith("resourcePacks:")).ToList() : [];
        // Minecraft 1.8.9's list is applied low-to-high. UI shows highest first.
        lines.Add("resourcePacks:" + JsonSerializer.Serialize(store.Current.Packs.AsEnumerable().Reverse())); ProfileStore.AtomicWrite(path, string.Join("\n", lines) + "\n");
    }
    public async Task<SearchHit[]> SearchAsync(string query, string kind, CancellationToken ct)
    {
        var facets = kind == "mod" ? "[[\"versions:1.8.9\"],[\"project_type:mod\"],[\"categories:forge\"]]" : "[[\"versions:1.8.9\"],[\"project_type:resourcepack\"]]";
        var url = "https://api.modrinth.com/v2/search?limit=20&query=" + Uri.EscapeDataString(query) + "&facets=" + Uri.EscapeDataString(facets);
        var root = JsonNode.Parse(await Http.GetStringAsync(url, ct))!;
        return root["hits"]!.AsArray().Select(h => new SearchHit(h!["project_id"]!.GetValue<string>(), h["title"]!.GetValue<string>(), h["description"]!.GetValue<string>(), kind)).ToArray();
    }
    public async Task InstallProjectAsync(string id, string kind, CancellationToken ct, string? auditedVersion = null)
    {
        store.EnsureEditable(); var versions = JsonNode.Parse(await Http.GetStringAsync("https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(id) + "/version?game_versions=%5B%221.8.9%22%5D" + (kind == "mod" ? "&loaders=%5B%22forge%22%5D" : ""), ct))!.AsArray();
        var v = (auditedVersion is null ? versions.FirstOrDefault() : versions.FirstOrDefault(x => x!["id"]!.GetValue<string>() == auditedVersion)) ?? throw new InvalidOperationException("The audited compatible 1.8.9 release is unavailable.");
        var staging = Path.Combine(store.Root, "staging", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            var pending = new List<(string Path, string Kind, string Hash)>(); var seen = new HashSet<string>();
            async Task Stage(JsonNode version, string contentKind)
            {
                var vid = version["id"]!.GetValue<string>(); if (!seen.Add(vid)) return;
                if (!version["game_versions"]!.AsArray().Any(x => x!.GetValue<string>() == "1.8.9") || (contentKind == "mod" && !version["loaders"]!.AsArray().Any(x => x!.GetValue<string>() == "forge"))) throw new InvalidDataException("Dependency is not compatible with Forge 1.8.9.");
                foreach (var dep in version["dependencies"]!.AsArray().Where(d => d!["dependency_type"]!.GetValue<string>() == "required"))
                {
                    if (dep!["version_id"] is null) throw new InvalidOperationException("Dependency is not pinned by the provider. Install it explicitly before retrying.");
                    var dv = JsonNode.Parse(await Http.GetStringAsync("https://api.modrinth.com/v2/version/" + Uri.EscapeDataString(dep["version_id"]!.GetValue<string>()), ct))!; await Stage(dv, "mod");
                }
                var f = version["files"]!.AsArray().FirstOrDefault(x => x!["primary"]?.GetValue<bool>() == true) ?? version["files"]!.AsArray().First()!;
                var name = ProfileStore.SafeName(f!["filename"]!.GetValue<string>()); var uri = new Uri(f["url"]!.GetValue<string>());
                if (uri.Scheme != "https" || uri.Host != "cdn.modrinth.com") throw new InvalidDataException("Unsupported download origin.");
                if (f["size"]!.GetValue<long>() > 256L * 1024 * 1024) throw new InvalidDataException("Content exceeds 256 MiB.");
                var path = Path.Combine(staging, name); await DownloadAsync(uri, path, ct);
                var bytes = await File.ReadAllBytesAsync(path, ct); var actual = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
                if (actual != f["hashes"]!["sha512"]!.GetValue<string>()) throw new InvalidDataException("Download hash mismatch.");
                ValidateArchive(path, contentKind, true); pending.Add((path, contentKind, ProfileStore.Hash(path)));
            }
            await Stage(v, kind); ct.ThrowIfCancellationRequested();
            // Preflight every collision before installing any member of a dependency group.
            foreach (var p in pending)
            {
                var target = Path.Combine(DirectoryFor(p.Kind), Path.GetFileName(p.Path));
                if (File.Exists(target + ".disabled")) throw new InvalidOperationException("A disabled copy exists; enable or delete it first.");
                if (File.Exists(target) && ProfileStore.Hash(target) != p.Hash) throw new IOException("An existing file differs from the provider copy. It has been preserved.");
            }
            var added = new List<string>(); var oldPacks = store.Current.Packs.ToList(); var oldManaged = new Dictionary<string, string>(store.Current.ManagedMods);
            try
            {
                foreach (var p in pending) { var dest = Path.Combine(DirectoryFor(p.Kind), Path.GetFileName(p.Path)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); if (!File.Exists(dest)) { File.Copy(p.Path, dest); added.Add(dest); } if (p.Kind == "mod") store.Current.ManagedMods[Path.GetFileName(dest)] = p.Hash; else if (!store.Current.Packs.Contains(Path.GetFileName(dest))) store.Current.Packs.Insert(0, Path.GetFileName(dest)); }
                SyncPacks(); store.Save();
            }
            catch { foreach (var path in added) File.Delete(path); store.Current.Packs = oldPacks; store.Current.ManagedMods = oldManaged; SyncPacks(); throw; }
        }
        finally { Directory.Delete(staging, true); }
    }
    public static async Task DownloadAsync(Uri uri, string path, CancellationToken ct)
    {
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(ct); await using var output = File.Create(path); var buffer = new byte[65536]; long total = 0; int n;
        while ((n = await input.ReadAsync(buffer, ct)) > 0) { total += n; if (total > 256L * 1024 * 1024) throw new InvalidDataException("Download limit exceeded."); await output.WriteAsync(buffer.AsMemory(0, n), ct); }
    }
}

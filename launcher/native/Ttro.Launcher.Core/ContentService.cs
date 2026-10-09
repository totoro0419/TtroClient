using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace Ttro.Launcher.Core;

public sealed class ContentService
{
    private readonly ProfileStore store;
    private readonly IContentProvider provider;
    private readonly HttpClient http;
    private readonly SemaphoreSlim mutation = new(1, 1);
    private static readonly HttpClient Http = CreateClient();
    public ContentService(ProfileStore store, IContentProvider? provider = null, HttpClient? http = null)
    {
        this.store = store; this.provider = provider ?? new ModrinthProvider(); this.http = http ?? Http;
        RecoverTransactions();
    }
    public static HttpClient CreateClient()
    {
        var h = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("TtroClient/0.3.0-alpha.1 (https://github.com/totoro0419/TtroClient)"); return h;
    }
    public static void ValidateKind(string kind) { if (kind is not ("mod" or "resourcepack")) throw new ArgumentException("Unknown content kind."); }
    public static bool Compatible(ContentVersion v, string kind) => v.GameVersions.Contains("1.8.9") && (kind != "mod" || v.Loaders.Contains("forge"));
    public string DirectoryFor(string kind) => DirectoryFor(store.Current, kind);
    private string DirectoryFor(Profile profile, string kind) { ValidateKind(kind); return Path.Combine(store.GameDirectory(profile), kind == "mod" ? "mods" : "resourcepacks"); }
    public string[] Files(string kind) => Directory.Exists(DirectoryFor(kind)) ? Directory.GetFiles(DirectoryFor(kind)).Where(p =>
        (kind == "mod" ? p.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) : p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) && !Path.GetFileName(p).StartsWith("ttro-client-1.8.9-", StringComparison.Ordinal)).Select(p => Path.GetFileName(p)).ToArray()! : [];
    public InstalledContent[] Installed(string kind)
    {
        var files = Files(kind); var ordered = kind == "resourcepack" ? store.Current.Packs.Where(files.Contains).Concat(files.Where(f => !store.Current.Packs.Contains(f))) : files.Order(StringComparer.OrdinalIgnoreCase);
        return ordered.Select(f => new InstalledContent(f, kind, kind == "mod" ? !f.EndsWith(".disabled", StringComparison.Ordinal) : store.Current.Packs.Contains(f), kind == "resourcepack" && store.Current.Packs.Contains(f) ? store.Current.Packs.IndexOf(f) + 1 : null, store.Current.Content.FirstOrDefault(c => c.Kind == kind && c.File == f))).ToArray();
    }
    private void Editable() { store.EnsureEditable(); if (mutation.CurrentCount == 0) throw new InvalidOperationException("Wait for the current content installation to finish."); }
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
            if (e.Length > 512 * 1024) throw new InvalidDataException("Pack metadata exceeds the limit.");
            using var r = new StreamReader(e.Open()); var meta = JsonNode.Parse(r.ReadToEnd());
            if (meta?["pack"]?["pack_format"]?.GetValue<int>() != 1) throw new InvalidDataException("This pack is not declared for Minecraft 1.8.9 (pack_format 1).");
        }
        else
        {
            if (zip.GetEntry("fabric.mod.json") is not null || zip.GetEntry("META-INF/mods.toml") is not null || zip.GetEntry("quilt.mod.json") is not null) throw new InvalidDataException("This is not a Forge 1.8.9 mod.");
            var meta = zip.GetEntry("mcmod.info");
            if (meta is null && !verifiedUpstreamMetadata) throw new InvalidDataException("A locally imported mod needs Forge 1.8.9 metadata.");
            if (meta is not null && meta.Length > 512 * 1024) throw new InvalidDataException("Mod metadata exceeds the limit.");
            if (meta is not null) { using var r = new StreamReader(meta.Open()); var text = r.ReadToEnd(); if (text.Contains("\"mousetweaks\"")) throw new InvalidDataException("Mouse Tweaks is already integrated into Ttro Client."); if (!verifiedUpstreamMetadata && !text.Contains("1.8.9")) throw new InvalidDataException("The mod does not declare Minecraft 1.8.9."); }
            var header = new byte[8];
            foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".class"))) { using var stream = e.Open(); stream.ReadExactly(header); if ( header[0] == 0xca && header[1] == 0xfe && header[2] == 0xba && header[3] == 0xbe && (header[6] * 256 + header[7]) > 52) throw new InvalidDataException("This mod requires a newer Java version than Java 8."); }
        }
    }
    public void Import(string file, string kind)
    {
        Editable(); ValidateKind(kind); var name = ProfileStore.SafeName(Path.GetFileName(file));
        if (kind == "mod" && !name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || kind == "resourcepack" && !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Select a .jar mod or .zip resource pack.");
        ValidateArchive(file, kind); Directory.CreateDirectory(DirectoryFor(kind)); var target = Path.Combine(DirectoryFor(kind), name);
        if (File.Exists(target) || File.Exists(target + ".disabled")) throw new IOException("This filename already exists. Remove or rename the existing content first.");
        var oldPacks = store.Current.Packs.ToList(); var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.Copy(file, temp); File.Move(temp, target); if (kind == "resourcepack") { store.Current.Packs.Insert(0, name); SyncPacks(); } store.Save(); }
        catch { File.Delete(temp); File.Delete(target); store.Current.Packs = oldPacks; if (kind == "resourcepack") SyncPacks(); throw; }
    }
    public void Toggle(string name, string kind)
    {
        Editable(); ProfileStore.SafeName(name); var path = Path.Combine(DirectoryFor(kind), name);
        if (kind == "mod")
        {
            var other = name.EndsWith(".disabled", StringComparison.Ordinal) ? path[..^9] : path + ".disabled";
            if (File.Exists(other)) throw new IOException("Content destination already exists.");
            var managed = store.Current.Content.FirstOrDefault(c => c.Kind == kind && c.File == name);
            File.Move(path, other);
            try { if (managed is not null) managed.File = Path.GetFileName(other); store.Save(); }
            catch { File.Move(other, path); if (managed is not null) managed.File = name; throw; }
        }
        else
        {
            if (!File.Exists(path)) throw new FileNotFoundException(); var old = store.Current.Packs.ToList();
            try { if (!store.Current.Packs.Remove(name)) store.Current.Packs.Insert(0, name); SyncPacks(); store.Save(); }
            catch { store.Current.Packs = old; SyncPacks(); throw; }
        }
    }
    public void MovePack(string name, int delta)
    {
        Editable(); var list = store.Current.Packs; var i = list.IndexOf(name); if (i < 0) throw new InvalidOperationException("Enable this pack before reordering."); var j = Math.Clamp(i + delta, 0, list.Count - 1);
        (list[i], list[j]) = (list[j], list[i]); try { SyncPacks(); store.Save(); } catch { (list[i], list[j]) = (list[j], list[i]); SyncPacks(); throw; }
    }
    public void Delete(string name, string kind)
    {
        Editable(); ProfileStore.SafeName(name); var path = Path.Combine(DirectoryFor(kind), name);
        var record = store.Current.Content.FirstOrDefault(c => c.Kind == kind && c.File == name);
        if (record is not null && store.Current.Content.Any(c => c != record && c.RequiredVersions.Contains(record.VersionId))) throw new InvalidOperationException("Another installed mod requires this dependency. Remove its dependent mod first.");
        var backup = store.Backup(path); var oldPacks = store.Current.Packs.ToList(); var oldManaged = new Dictionary<string, string>(store.Current.ManagedMods);
        try { File.Delete(path); store.Current.Packs.Remove(name); if (kind == "mod") store.Current.ManagedMods.Remove(name.Replace(".disabled", "")); if (record is not null) store.Current.Content.Remove(record); if (kind == "resourcepack") SyncPacks(); store.Save(); }
        catch { if (!File.Exists(path) && File.Exists(backup)) File.Copy(backup, path); store.Current.Packs = oldPacks; store.Current.ManagedMods = oldManaged; if (record is not null && !store.Current.Content.Contains(record)) store.Current.Content.Add(record); if (kind == "resourcepack") SyncPacks(); throw; }
    }
    public void SyncPacks() => SyncPacks(store.Current);
    private void SyncPacks(Profile profile)
    {
        var path = Path.Combine(store.GameDirectory(profile), "options.txt");
        var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith("resourcePacks:")).ToList() : [];
        // Minecraft 1.8.9 applies low-to-high; the UI shows highest first.
        lines.Add("resourcePacks:" + JsonSerializer.Serialize(profile.Packs.AsEnumerable().Reverse())); ProfileStore.AtomicWrite(path, string.Join("\n", lines) + "\n");
    }
    public Task<ContentPage> BrowseAsync(ContentQuery query, CancellationToken ct) => provider.SearchAsync(query, ct);
    public async Task<SearchHit[]> SearchAsync(string query, string kind, CancellationToken ct) => (await BrowseAsync(new(query, kind, "relevance"), ct)).Hits;
    public Task<ContentDetails> DetailsAsync(string id, string kind, CancellationToken ct) => provider.DetailsAsync(id, kind, ct);
    public async Task<Dictionary<string, ContentVersion>> CheckUpdatesAsync(string kind, CancellationToken ct)
    {
        var profile = store.Current; var result = new Dictionary<string, ContentVersion>();
        foreach (var record in profile.Content.Where(c => c.Kind == kind && c.Provider == provider.Id).ToArray())
        {
            ct.ThrowIfCancellationRequested(); var versions = await provider.VersionsAsync(record.ProjectId, kind, ct).ConfigureAwait(false);
            var latest = Latest(versions, kind); if (latest is not null && latest.Id != record.VersionId && latest.Published > record.Published) result[record.ProjectId] = latest;
        }
        if (store.Current != profile) throw new OperationCanceledException("The profile changed."); return result;
    }
    public async Task<ContentVersion?> LatestVersionAsync(string id, string kind, CancellationToken ct) => Latest(await provider.VersionsAsync(id, kind, ct).ConfigureAwait(false), kind);
    private static ContentVersion? Latest(ContentVersion[] versions, string kind) => versions.Where(v => Compatible(v, kind) && v.VersionType == "release").OrderByDescending(v => v.Published).FirstOrDefault();
    public Task InstallProjectAsync(string id, string kind, CancellationToken ct, string? auditedVersion = null) => InstallAsync(id, kind, ct, auditedVersion, false, null);
    public Task InstallAsync(string id, string kind, CancellationToken ct, string? version = null, bool update = false, IProgress<ContentProgress>? progress = null) => InstallCoreAsync(id, kind, ct, version, update, progress);
    public Task UpdateAsync(InstalledContentRecord record, CancellationToken ct, IProgress<ContentProgress>? progress = null) => InstallCoreAsync(record.ProjectId, record.Kind, ct, null, true, progress);
    private static string Sha512(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA512.HashData(stream)).ToLowerInvariant(); }
    private sealed record Pending(string Staged, InstalledContentRecord Record, InstalledContentRecord? Old);
    public sealed class FileChange
    {
        public string Kind { get; set; } = "";
        public string Target { get; set; } = "";
        public string? Old { get; set; }
        public string? Rollback { get; set; }
        public string? Backup { get; set; }
    }
    public sealed class Transaction
    {
        public bool Committed { get; set; }
        public List<string> Packs { get; set; } = [];
        public Dictionary<string, string> ManagedMods { get; set; } = [];
        public List<InstalledContentRecord> Content { get; set; } = [];
        public string? Options { get; set; }
        public List<FileChange> Changes { get; set; } = [];
    }
    private async Task InstallCoreAsync(string id, string kind, CancellationToken ct, string? auditedVersion, bool update, IProgress<ContentProgress>? progress)
    {
        ValidateKind(kind); store.EnsureEditable(); await mutation.WaitAsync(ct).ConfigureAwait(false);
        var profile = store.Current; var staging = Path.Combine(store.GameDirectory(profile), ".content-staging", Guid.NewGuid().ToString("N"));
        try
        {
            var oldMain = profile.Content.SingleOrDefault(c => c.Provider == provider.Id && c.ProjectId == id && c.Kind == kind);
            if (update && oldMain is null) throw new InvalidOperationException("Only provider-managed content can be updated.");
            progress?.Report(new("Checking compatible releases", id));
            var versions = await provider.VersionsAsync(id, kind, ct).ConfigureAwait(false);
            var v = (auditedVersion is null ? Latest(versions, kind) : versions.FirstOrDefault(x => x.Id == auditedVersion)) ?? throw new InvalidOperationException("No compatible stable 1.8.9 release is available.");
            if (update && oldMain is not null && (v.Id == oldMain.VersionId || v.Published <= oldMain.Published)) return;
            Directory.CreateDirectory(staging); var pending = new List<Pending>(); var seen = new Dictionary<string, string>();
            async Task Stage(ContentVersion version, string contentKind, string title)
            {
                if (seen.TryGetValue(version.ProjectId, out var visited)) { if (visited != version.Id) throw new InvalidDataException("Conflicting pinned dependency versions."); return; }
                if (seen.Count >= 32) throw new InvalidDataException("Dependency group exceeds the limit."); seen.Add(version.ProjectId, version.Id);
                if (!Compatible(version, contentKind)) throw new InvalidDataException("Dependency is not compatible with Forge 1.8.9.");
                foreach (var incompatible in version.Dependencies.Where(d => d.Type == "incompatible"))
                    if (profile.Content.Any(c => c.ProjectId == incompatible.ProjectId || c.VersionId == incompatible.VersionId)) throw new InvalidDataException("An incompatible managed mod is installed. Remove it before installing this project.");
                foreach (var dep in version.Dependencies.Where(d => d.Type == "required"))
                {
                    if (dep.VersionId is null) throw new InvalidOperationException("The provider has not pinned a required dependency version. No files were changed.");
                    var dv = await provider.VersionAsync(dep.VersionId, ct).ConfigureAwait(false);
                    if (dv.Id != dep.VersionId || dep.ProjectId is not null && dv.ProjectId != dep.ProjectId) throw new InvalidDataException("Dependency identity mismatch.");
                    await Stage(dv, "mod", dv.Name).ConfigureAwait(false);
                }
                var f = version.Files.FirstOrDefault(x => x.Primary) ?? (version.Files.Length == 1 ? version.Files[0] : throw new InvalidDataException("Provider has no unambiguous primary file."));
                var name = ProfileStore.SafeName(f.Filename); if (!name.EndsWith(contentKind == "mod" ? ".jar" : ".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Provider file extension mismatch.");
                var uri = TrustedDownload(f.Url);
                if (f.Size is < 1 or > 268435456 || f.Sha512.Length != 128 || !f.Sha512.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid provider size or SHA-512 metadata.");
                var old = profile.Content.SingleOrDefault(c => c.Provider == provider.Id && c.ProjectId == version.ProjectId && c.Kind == contentKind);
                if (old is not null)
                {
                    var oldPath = Path.Combine(DirectoryFor(profile, contentKind), old.File);
                    if (!File.Exists(oldPath) || Sha512(oldPath) != old.Sha512) throw new InvalidDataException("The installed managed file is missing or was modified. It has been preserved.");
                    if (old.VersionId == version.Id) return;
                    if (!update) throw new InvalidOperationException("A managed version is already installed. Use UPDATE.");
                    if (profile.Content.Any(c => c != old && c.RequiredVersions.Contains(old.VersionId) && !seen.ContainsKey(c.ProjectId))) throw new InvalidOperationException("Another installed mod requires the current pinned dependency. Update its dependent mod first.");
                    if (old.File.EndsWith(".disabled", StringComparison.Ordinal)) name += ".disabled";
                }
                var path = Path.Combine(staging, pending.Count + "-download"); var lastFeedback = Environment.TickCount64;
                progress?.Report(new("Downloading", title, 0, f.Size));
                await DownloadWithAsync(http, uri, path, ct, n => { if (n == f.Size || Environment.TickCount64 - lastFeedback >= 100) { lastFeedback = Environment.TickCount64; progress?.Report(new("Downloading", title, n, f.Size)); } }).ConfigureAwait(false);
                progress?.Report(new("Verifying SHA-512 and archive", title));
                if (new FileInfo(path).Length != f.Size || Sha512(path) != f.Sha512.ToLowerInvariant()) throw new InvalidDataException("Download hash or size mismatch.");
                ValidateArchive(path, contentKind, true);
                var record = new InstalledContentRecord { Provider = provider.Id, ProjectId = version.ProjectId, VersionId = version.Id, VersionNumber = version.Number, Published = version.Published, Kind = contentKind, Title = title, File = name, Sha512 = f.Sha512.ToLowerInvariant(), RequiredVersions = version.Dependencies.Where(d => d.Type == "required").Select(d => d.VersionId!).ToArray() };
                pending.Add(new(path, record, old));
            }
            if (v.ProjectId != id && auditedVersion is null)
            {
                // Slugs are accepted at entry, but persistent identity is always the provider ID.
                var details = await provider.DetailsAsync(id, kind, ct).ConfigureAwait(false); if (details.Project.ProjectId != v.ProjectId) throw new InvalidDataException("Project identity mismatch.");
            }
            await Stage(v, kind, (await provider.DetailsAsync(v.ProjectId, kind, ct).ConfigureAwait(false)).Project.Title).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested(); store.EnsureEditable(); if (store.Current != profile) throw new OperationCanceledException("The profile changed before installation.");
            if (pending.Count == 0) return;
            // Validate all destination collisions before mutating any dependency-group member.
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in pending)
            {
                var target = Path.Combine(DirectoryFor(profile, p.Record.Kind), p.Record.File);
                if (!targets.Add(target)) throw new IOException("Two provider files have the same destination.");
                var enabledName = p.Record.File.EndsWith(".disabled", StringComparison.Ordinal) ? p.Record.File[..^9] : p.Record.File;
                foreach (var candidate in new[] { enabledName, enabledName + ".disabled" })
                    if ((File.Exists(Path.Combine(DirectoryFor(profile, p.Record.Kind), candidate)) || Directory.Exists(Path.Combine(DirectoryFor(profile, p.Record.Kind), candidate))) && (p.Old is null || !candidate.Equals(p.Old.File, StringComparison.OrdinalIgnoreCase))) throw new IOException("A local or unrelated file has the same name. It has been preserved.");
            }
            var optionsPath = Path.Combine(store.GameDirectory(profile), "options.txt");
            var transaction = new Transaction { Packs = profile.Packs.ToList(), ManagedMods = new(profile.ManagedMods), Content = JsonSerializer.Deserialize<List<InstalledContentRecord>>(JsonSerializer.Serialize(profile.Content))!, Options = File.Exists(optionsPath) ? File.ReadAllText(optionsPath) : null };
            foreach (var p in pending)
            {
                var change = new FileChange { Kind = p.Record.Kind, Target = p.Record.File, Old = p.Old?.File };
                if (p.Old is not null)
                {
                    change.Rollback = transaction.Changes.Count + "-rollback"; File.Copy(Path.Combine(DirectoryFor(profile, p.Record.Kind), p.Old.File), Path.Combine(staging, change.Rollback));
                    change.Backup = Guid.NewGuid().ToString("N") + "-" + p.Old.File;
                }
                transaction.Changes.Add(change);
            }
            var journal = Path.Combine(staging, "transaction.json"); ProfileStore.AtomicWrite(journal, JsonSerializer.Serialize(transaction));
            try
            {
                progress?.Report(new("Installing into " + profile.Name, v.Name));
                // Cancellation stops before commit. The short synchronous commit is rolled back as a group on failure.
                var newPacks = profile.Packs.ToList(); var newContent = profile.Content.ToList(); var newManaged = new Dictionary<string, string>(profile.ManagedMods);
                foreach (var p in pending)
                {
                    var dest = Path.Combine(DirectoryFor(profile, p.Record.Kind), p.Record.File); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Move(p.Staged, dest, true);
                    if (p.Old is not null) { newContent.Remove(p.Old); if (p.Record.Kind == "resourcepack") { var index = newPacks.IndexOf(p.Old.File); if (index >= 0) newPacks[index] = p.Record.File; } else newManaged.Remove(p.Old.File.Replace(".disabled", "")); }
                    else if (p.Record.Kind == "resourcepack") newPacks.Insert(0, p.Record.File);
                    if (p.Record.Kind == "mod") newManaged[p.Record.File.Replace(".disabled", "")] = ProfileStore.Hash(dest);
                    newContent.Add(p.Record);
                }
                profile.Packs = newPacks; profile.Content = newContent; profile.ManagedMods = newManaged;
                SyncPacks(profile); store.Save(); transaction.Committed = true; ProfileStore.AtomicWrite(journal, JsonSerializer.Serialize(transaction));
            }
            catch { Restore(profile, staging, transaction); throw; }
            Finish(profile, staging, transaction); progress?.Report(new("Installed and verified", v.Name));
        }
        finally { if (Directory.Exists(staging) && !File.Exists(Path.Combine(staging, "transaction.json"))) Directory.Delete(staging, true); mutation.Release(); }
    }
    private void Restore(Profile profile, string staging, Transaction transaction)
    {
        foreach (var change in transaction.Changes)
        {
            var dir = DirectoryFor(profile, change.Kind); var target = Path.Combine(dir, ProfileStore.SafeName(change.Target));
            if (change.Rollback is not null && change.Old is not null) { Directory.CreateDirectory(dir); File.Copy(Path.Combine(staging, ProfileStore.SafeName(change.Rollback)), Path.Combine(dir, ProfileStore.SafeName(change.Old)), true); }
            if (change.Old is null || !change.Old.Equals(change.Target, StringComparison.OrdinalIgnoreCase)) File.Delete(target);
        }
        profile.Packs = transaction.Packs; profile.ManagedMods = transaction.ManagedMods; profile.Content = transaction.Content;
        var options = Path.Combine(store.GameDirectory(profile), "options.txt"); if (transaction.Options is null) File.Delete(options); else ProfileStore.AtomicWrite(options, transaction.Options);
        store.Save(); Directory.Delete(staging, true);
    }
    private void Finish(Profile profile, string staging, Transaction transaction)
    {
        foreach (var change in transaction.Changes.Where(c => c.Old is not null))
        {
            var backupDir = Path.Combine(store.Root, "backups"); Directory.CreateDirectory(backupDir);
            var backup = Path.Combine(backupDir, ProfileStore.SafeName(change.Backup!));
            if (!File.Exists(backup)) File.Copy(Path.Combine(staging, ProfileStore.SafeName(change.Rollback!)), backup);
            if (!change.Old!.Equals(change.Target, StringComparison.OrdinalIgnoreCase)) File.Delete(Path.Combine(DirectoryFor(profile, change.Kind), ProfileStore.SafeName(change.Old)));
        }
        Directory.Delete(staging, true);
    }
    private void RecoverTransactions()
    {
        foreach (var profile in store.State.Profiles)
        {
            var dir = Path.Combine(store.GameDirectory(profile), ".content-staging"); if (!Directory.Exists(dir)) continue;
            foreach (var staging in Directory.GetDirectories(dir))
            {
                var path = Path.Combine(staging, "transaction.json");
                if (!File.Exists(path)) { Directory.Delete(staging, true); continue; }
                var transaction = JsonSerializer.Deserialize<Transaction>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid content recovery journal; files were preserved.");
                if (transaction.Committed) Finish(profile, staging, transaction); else Restore(profile, staging, transaction);
            }
        }
    }
    public static Uri TrustedDownload(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cdn.modrinth.com" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) throw new InvalidDataException("Unsupported download origin."); return uri;
    }
    public static Task DownloadAsync(Uri uri, string path, CancellationToken ct) => DownloadWithAsync(Http, uri, path, ct, null);
    private static async Task DownloadWithAsync(HttpClient client, Uri uri, string path, CancellationToken ct, Action<long>? progress)
    {
        TrustedDownload(uri.AbsoluteUri);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Host != uri.Host || response.Content.Headers.ContentLength > 256L * 1024 * 1024) throw new InvalidDataException("Unsafe redirect or content size.");
        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false); await using var output = File.Create(path); var buffer = new byte[65536]; long total = 0; int n;
        while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0) { total += n; if (total > 256L * 1024 * 1024) throw new InvalidDataException("Download limit exceeded."); await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false); progress?.Invoke(total); }
        await output.FlushAsync(ct).ConfigureAwait(false);
    }
}

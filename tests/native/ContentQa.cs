using Ttro.Launcher.Core;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

public static class ContentQa
{
    public static async Task Run(string catalog)
    {
        var root = Path.Combine(Path.GetTempPath(), "ttro-content-" + Guid.NewGuid().ToString("N")); var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); Console.WriteLine("PASS " + name); }
        async Task Reject(Func<Task> action, string name) { try { await action(); } catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { Check(true, name); return; } throw new Exception("Accepted: " + name); }
        try
        {
            var store = new ProfileStore(root, catalog); var fixture = new ContentFixture(); var service = new ContentService(store, fixture, fixture.Http);
            var page = await service.BrowseAsync(new("", "resourcepack"), default); Check(page.Hits.Length == 12 && page.Hits[0].Author == "QA creator" && page.Hits[0].IconUrl is not null, "empty-query browse returns image and provider metadata");
            page = await service.BrowseAsync(new("pack1", "resourcepack", "updated", 12, 12, "combat", "16x"), default); Check(fixture.LastQuery!.Offset == 12 && fixture.LastQuery.Category == "combat", "pagination and provider filters retained");
            var options = Path.Combine(store.GameDirectory(store.Current), "options.txt"); ProfileStore.AtomicWrite(options, "renderDistance:8\nresourcePacks:[]\n");
            await service.InstallProjectAsync("pack0", "resourcepack", default); var record = store.Current.Content.Single();
            var file = Path.Combine(service.DirectoryFor("resourcepack"), record.File); var bytes = File.ReadAllBytes(file);
            Check(Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant() == record.Sha512, "installed SHA-512 exactly matches provider");
            using (var zip = ZipFile.OpenRead(file)) Check(zip.GetEntry("pack.mcmeta") is not null && zip.GetEntry("pack.png") is not null, "validated pack.mcmeta and preview preserved");
            Check(store.Current.Packs[0] == record.File && File.ReadAllText(options).Contains(record.File) && File.ReadAllText(options).Contains("renderDistance:8"), "install enables pack and preserves unrelated Minecraft options");
            var reloaded = new ProfileStore(root, catalog); Check(reloaded.Current.Content.Single().VersionId == "pack0v1", "managed provider/project/version/file/hash persists");
            var details = await service.DetailsAsync("pack0", "resourcepack", default); Check(details.Project.Gallery.Length == 1 && details.Project.License == "MIT", "details expose provider gallery/license");
            service.Toggle(record.File, "resourcepack"); fixture.Revision = 2;
            var updates = await service.CheckUpdatesAsync("resourcepack", default); Check(updates["pack0"].Id == "pack0v2", "compatible managed update detected");
            await service.UpdateAsync(record, default); record = store.Current.Content.Single();
            Check(record.VersionId == "pack0v2" && !store.Current.Packs.Contains(record.File), "disabled pack remains disabled after update");
            Check(!File.Exists(file) && Directory.GetFiles(Path.Combine(root, "backups")).Any(p => File.ReadAllBytes(p).SequenceEqual(bytes)), "old pack backed up only after successful replacement");
            service.Toggle(record.File, "resourcepack"); await service.InstallProjectAsync("pack1", "resourcepack", default); var oldPriority = store.Current.Packs.IndexOf(record.File);
            fixture.Revision = 3; await service.UpdateAsync(record, default); record = store.Current.Content.Single(c => c.ProjectId == "pack0"); Check(store.Current.Packs.IndexOf(record.File) == oldPriority, "enabled pack update preserves priority");
            var before = JsonSerializer.Serialize(store.Current); var current = Path.Combine(service.DirectoryFor("resourcepack"), record.File); var original = File.ReadAllBytes(current); fixture.Revision = 4; fixture.Corrupt = true;
            await Reject(() => service.UpdateAsync(record, default), "hash mismatch rejects update"); Check(File.ReadAllBytes(current).SequenceEqual(original) && JsonSerializer.Serialize(store.Current) == before, "hash failure retains installed file/state"); fixture.Corrupt = false;
            fixture.FailDownload = true; await Reject(() => service.UpdateAsync(record, default), "download failure rejects update"); Check(File.ReadAllBytes(current).SequenceEqual(original), "download failure retains current usable pack"); fixture.FailDownload = false;
            fixture.Slow = true; using (var cancel = new CancellationTokenSource(50)) await Reject(() => service.UpdateAsync(record, cancel.Token), "download cancellation interrupts before commit"); fixture.Slow = false;
            Check(File.ReadAllBytes(current).SequenceEqual(original) && !Directory.GetFiles(Path.Combine(store.GameDirectory(store.Current), ".content-staging"), "transaction.json", SearchOption.AllDirectories).Any(), "cancel cleans staging and preserves content");
            File.AppendAllText(current, "edited"); await Reject(() => service.UpdateAsync(record, default), "modified managed pack update rejected"); File.WriteAllBytes(current, original);
            fixture.WrongVersion = true; await Reject(() => service.InstallProjectAsync("modern", "resourcepack", default), "wrong Minecraft provider release rejected"); fixture.WrongVersion = false;
            fixture.WrongLoader = true; await Reject(() => service.InstallProjectAsync("fabric", "mod", default), "Fabric provider version rejected"); fixture.WrongLoader = false;
            fixture.Unpinned = true; await Reject(() => service.InstallProjectAsync("dependent", "mod", default), "unpinned required dependency rejected"); fixture.Unpinned = false;
            await service.InstallProjectAsync("dependent", "mod", default);
            Check(store.Current.Content.Any(c => c.ProjectId == "dependency") && store.Current.Content.Any(c => c.ProjectId == "dependent" && c.RequiredVersions.Contains("dependencyv1")), "pinned dependency group installs and records ownership");
            var dep = store.Current.Content.Single(c => c.ProjectId == "dependency"); await Reject(() => Task.Run(() => service.Delete(dep.File, "mod")), "required installed dependency cannot be removed");
            var mod = store.Current.Content.Single(c => c.ProjectId == "dependent"); service.Toggle(mod.File, "mod"); fixture.Revision = 5; await service.UpdateAsync(mod, default); mod = store.Current.Content.Single(c => c.ProjectId == "dependent"); Check(mod.File.EndsWith(".disabled") && !service.Installed("mod").Single(x => x.Managed?.ProjectId == "dependent").Enabled, "disabled mod stays disabled after managed update");
            fixture.Offline = true; service.Toggle(record.File, "resourcepack"); service.Toggle(mod.File, "mod"); Check(service.Installed("mod").Length == 2, "offline installed enable/disable and listing work"); fixture.Offline = false;
            fixture.Revision = 1; var local = Path.Combine(root, "pack-local.zip"); File.WriteAllBytes(local, fixture.Archive("local", "resourcepack", 1)); service.Import(local, "resourcepack"); Check(service.Installed("resourcepack").Single(x => x.File == "pack-local.zip").Managed is null, "local import stays local");
            var collision = Path.Combine(root, "pack9-1.zip"); File.WriteAllBytes(collision, fixture.Archive("local9", "resourcepack", 1)); service.Import(collision, "resourcepack"); await Reject(() => service.InstallProjectAsync("pack9", "resourcepack", default), "same-name local collision preserved"); Check(File.ReadAllBytes(Path.Combine(service.DirectoryFor("resourcepack"), "pack9-1.zip")).SequenceEqual(File.ReadAllBytes(collision)), "collision retains local bytes");
            var profile = store.Current; fixture.Slow = true;
            var installing = service.InstallProjectAsync("pack8", "resourcepack", default); await Task.Delay(100); store.Add("Other profile"); await Reject(() => installing, "profile change rejects stale installation"); fixture.Slow = false;
            Check(service.Installed("resourcepack").Length == 0 && !profile.Content.Any(c => c.ProjectId == "pack8"), "profiles do not mix content"); store.Select(profile);
            service.Delete("pack-local.zip", "resourcepack"); Check(!File.Exists(Path.Combine(service.DirectoryFor("resourcepack"), "pack-local.zip")) && Directory.GetFiles(Path.Combine(root, "backups")).Any(p => p.EndsWith("pack-local.zip")), "remove keeps local-file backup");
            // Crash-like interrupted commit: persisted journal, staged new version, changed metadata and options.
            var recovery = Path.Combine(store.GameDirectory(profile), ".content-staging", "crash-fixture"); Directory.CreateDirectory(recovery); File.WriteAllBytes(Path.Combine(recovery, "old-backup"), original);
            var journal = new ContentService.Transaction { Packs = profile.Packs.ToList(), Content = JsonSerializer.Deserialize<List<InstalledContentRecord>>(JsonSerializer.Serialize(profile.Content))!, ManagedMods = new(profile.ManagedMods), Options = File.ReadAllText(options), Changes = [new() { Kind = "resourcepack", Target = record.File, Old = record.File, Rollback = "old-backup", Backup = "safe-backup.zip" }] };
            ProfileStore.AtomicWrite(Path.Combine(recovery, "transaction.json"), JsonSerializer.Serialize(journal)); File.WriteAllText(current, "partial replacement"); profile.Packs.Clear(); store.Save(); File.WriteAllText(options, "partial options");
            service = new ContentService(store, fixture, fixture.Http); Check(File.ReadAllBytes(current).SequenceEqual(original) && File.ReadAllText(options) == journal.Options && profile.Packs.SequenceEqual(journal.Packs), "startup journal recovery restores pack/state/options after interrupted commit");
            string Zip(string filename, Dictionary<string, byte[]> entries) { var p = Path.Combine(root, filename); using var zip = ZipFile.Open(p, ZipArchiveMode.Create); foreach (var pair in entries) { using var s = zip.CreateEntry(pair.Key).Open(); s.Write(pair.Value); } return p; }
            var java = Zip("java17.jar", new() { ["mcmod.info"] = System.Text.Encoding.UTF8.GetBytes("[{\"mcversion\":\"1.8.9\"}]"), ["A.class"] = [0xca,0xfe,0xba,0xbe,0,0,0,61] }); await Reject(() => Task.Run(() => service.Import(java, "mod")), "Java above 8 rejected");
            var modern = Zip("new.zip", new() { ["pack.mcmeta"] = System.Text.Encoding.UTF8.GetBytes("{\"pack\":{\"pack_format\":34}}") }); await Reject(() => Task.Run(() => service.Import(modern, "resourcepack")), "new Minecraft archive rejected");
            await Reject(() => Task.Run(() => ContentService.TrustedDownload("https://example.com/mod.jar")), "foreign download origin rejected");
            Console.WriteLine($"Content QA: {checks.Count} checks passed.");
            var output = Environment.GetEnvironmentVariable("TTRO_CONTENT_QA_REPORT"); if (output is not null) ProfileStore.AtomicWrite(output, JsonSerializer.Serialize(new { environment = "Native .NET content service / deterministic provider and HTTP fixtures", checks, status = "PASS", limits = new[] { "Fixture provider is not live Modrinth", "No Microsoft account or Minecraft E2E in this test" } }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    }
}

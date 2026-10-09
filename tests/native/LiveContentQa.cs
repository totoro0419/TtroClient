using System.Text.Json;
using Ttro.Launcher.Core;

public static class LiveContentQa
{
    public static async Task Run(string catalog)
    {
        var root = Path.Combine(Path.GetTempPath(), "ttro-live-" + Guid.NewGuid().ToString("N"));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(4)); var ct = cancel.Token;
        var store = new ProfileStore(root, catalog); var service = new ContentService(store); var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks.Add(name); Console.WriteLine("PASS live " + name); }
        try
        {
            var packs = await service.BrowseAsync(new("", "resourcepack", "downloads"), ct);
            Check(packs.Hits.Length > 0 && packs.Hits.Any(h => h.IconUrl is not null), "live 1.8.9 browse supplies preinstall icons");
            var search = await service.SearchAsync(packs.Hits[0].Title, "resourcepack", ct); Check(search.Length > 0, "live search");
            var filtered = await service.BrowseAsync(new("", "resourcepack", "updated", Category:"combat", Resolution:"16x"), ct);
            Check(filtered.Hits.All(h => h.Categories.Contains("combat") && h.Categories.Contains("16x")), "provider-tag combat/16x filtering");
            var mods = await service.BrowseAsync(new("", "mod"), ct); Check(mods.Hits.Length > 0 && mods.Hits.All(h => h.Categories.Contains("forge")), "live Forge 1.8.9 mod browsing");
            SearchHit? selected = null;
            foreach (var hit in packs.Hits)
            {
                try { await service.InstallProjectAsync(hit.ProjectId, "resourcepack", ct); selected = hit; break; }
                catch (InvalidDataException ex) { Console.WriteLine("Provider/archive compatibility rejection: " + ex.Message); }
            }
            Check(selected is not null, "live provider resourcepack download/hash/archive/install");
            var record = store.Current.Content.Single(c => c.Kind == "resourcepack");
            Check(record.ProjectId == selected!.ProjectId && record.Sha512.Length == 128 && store.Current.Packs.Contains(record.File), "live pack recorded and enabled");
            Check(File.ReadAllText(Path.Combine(store.GameDirectory(store.Current), "options.txt")).Contains(record.File), "live pack Minecraft options registration");
            var details = await service.DetailsAsync(selected.ProjectId, "resourcepack", ct); Check(details.Project.ProjectId == selected.ProjectId, "live details project identity");
            await service.InstallProjectAsync("hypixel-mod-api", "mod", ct, "VtDhN4ZW"); Check(service.HasProvider("hypixel_mod_api"), "audited real Forge mod downloads and validates");
            await service.CheckUpdatesAsync("resourcepack", ct); checks.Add("live compatible update query");
            var output = Environment.GetEnvironmentVariable("TTRO_LIVE_CONTENT_QA_REPORT"); if (output is not null) ProfileStore.AtomicWrite(output, JsonSerializer.Serialize(new { status = "PASS", environment = "Live Modrinth HTTPS / native .NET", project = selected.ProjectId, record.File, record.Sha512, checks, limits = new[] { "No Microsoft account or Minecraft E2E in this test" } }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Live content QA: {checks.Count} checks passed.");
        }
        finally { Directory.Delete(root, true); }
    }
}

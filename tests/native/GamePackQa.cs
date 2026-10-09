using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Ttro.Launcher.Core;

public static class GamePackQa
{
    public static async Task Export(string catalog, string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "ttro-native-packs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fixture = new PacksProvider(); var store = new ProfileStore(root, catalog); var content = new ContentService(store, fixture, new HttpClient(new Handler(fixture)));
            await content.InstallProjectAsync("qa-low", "resourcepack", default); await content.InstallProjectAsync("qa-high", "resourcepack", default);
            content.MovePack("qa-pack-high.zip", 1); content.MovePack("qa-pack-high.zip", -1); // Explicit native reorder, highest first.
            Directory.CreateDirectory(Path.Combine(destination, "resourcepacks"));
            foreach (var file in Directory.GetFiles(content.DirectoryFor("resourcepack"))) File.Copy(file, Path.Combine(destination, "resourcepacks", Path.GetFileName(file)), true);
            File.Copy(Path.Combine(store.GameDirectory(store.Current), "options.txt"), Path.Combine(destination, "options.txt"), true);
            ProfileStore.AtomicWrite(Path.Combine(destination, "native-pack-provenance.json"), JsonSerializer.Serialize(new { source = "Shipping native ContentService.InstallProjectAsync / deterministic provider fixture", records = store.Current.Content, packsHighestFirst = store.Current.Packs, options = File.ReadAllText(Path.Combine(destination, "options.txt")), limitations = "QA fixture download transport; no authentication bypass in product" }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Native content-service pack group exported for real Minecraft QA.");
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class PacksProvider : IContentProvider
    {
        public string Id => "modrinth";
        public byte[] Bytes(string id) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, id == "qa-high" ? "qa-pack-high.zip" : "qa-pack-low.zip"));
        public Task<ContentPage> SearchAsync(ContentQuery q, CancellationToken ct) => throw new NotSupportedException();
        public Task<ContentDetails> DetailsAsync(string id, string kind, CancellationToken ct) => Task.FromResult(new ContentDetails(new(id, id, "Safe pack for actual Minecraft resource-manager QA", kind), "", ["1.8.9"]));
        public Task<ContentVersion[]> VersionsAsync(string id, string kind, CancellationToken ct)
        {
            var bytes = Bytes(id); var filename = id == "qa-high" ? "qa-pack-high.zip" : "qa-pack-low.zip";
            return Task.FromResult(new[] { new ContentVersion(id, id + "v1", "1", id, DateTimeOffset.Parse("2026-10-09T00:00:00Z"), "release", ["1.8.9"], ["minecraft"], [new(filename, "https://cdn.modrinth.com/qa/" + id, Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant(), bytes.Length, true)], []) });
        }
        public Task<ContentVersion> VersionAsync(string id, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Handler(PacksProvider provider) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(provider.Bytes(request.RequestUri!.Segments.Last())) });
    }
}

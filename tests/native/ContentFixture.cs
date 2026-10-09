using System.IO;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using Ttro.Launcher.Core;

public sealed class ContentFixture : IContentProvider
{
    public string Id => "modrinth";
    public int Revision = 1;
    public bool Offline, Corrupt, FailDownload, Slow, Unpinned, WrongVersion, WrongLoader, Collision, ConflictingDependency;
    public int Searches;
    public ContentQuery? LastQuery;
    public static readonly byte[] Icon = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    public HttpClient Http => new(new Handler(this));
    public byte[] Archive(string id, string kind, int revision)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Write(string name, byte[] bytes) { using var output = zip.CreateEntry(name).Open(); output.Write(bytes); }
            if (kind == "resourcepack") { Write("pack.mcmeta", System.Text.Encoding.UTF8.GetBytes("{\"pack\":{\"pack_format\":1,\"description\":\"QA " + revision + "\"}}")); Write("pack.png", Icon); }
            else { Write("mcmod.info", System.Text.Encoding.UTF8.GetBytes("[{\"modid\":\"" + id + "\",\"mcversion\":\"1.8.9\"}]")); Write("QA.class", new byte[] { 0xca, 0xfe, 0xba, 0xbe, 0, 0, 0, 52 }); }
            Write("version.txt", System.Text.Encoding.UTF8.GetBytes(id + revision));
        }
        return stream.ToArray();
    }
    public SearchHit Hit(string id, string kind) => new(id, "QA " + id, "Safe test content; provider metadata is a fixture.", kind) { Author = "QA creator", Downloads = 12345, Updated = DateTimeOffset.Parse("2026-10-09T00:00:00Z"), IconUrl = "https://cdn.modrinth.com/qa/icon.png", Gallery = ["https://cdn.modrinth.com/qa/icon.png"], Categories = kind == "resourcepack" ? ["combat", "16x"] : ["forge"], License = "MIT" };
    public Task<ContentPage> SearchAsync(ContentQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (Offline) throw new HttpRequestException("QA offline"); Searches++; LastQuery = query;
        var hits = Enumerable.Range(query.Offset, Math.Min(query.Limit, 15 - query.Offset)).Select(i => Hit((query.Kind == "mod" ? "mod" : "pack") + i, query.Kind)).Where(h => h.Title.Contains(query.Query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(new ContentPage(hits, query.Offset, 15));
    }
    public Task<ContentDetails> DetailsAsync(string id, string kind, CancellationToken ct) { ct.ThrowIfCancellationRequested(); if (Offline) throw new HttpRequestException("QA offline"); return Task.FromResult(new ContentDetails(Hit(id, kind), "QA description. No network or account bypass is used.", ["1.8.9"])); }
    public ContentVersion Version(string id, string kind, int revision)
    {
        var bytes = Archive(id, kind, revision);
        return new(id, id + "v" + revision, "1.0." + revision, "QA " + id, DateTimeOffset.Parse("2026-10-01T00:00:00Z").AddDays(revision), "release", WrongVersion ? ["1.21.1"] : ["1.8.9"], WrongLoader ? ["fabric"] : kind == "mod" ? ["forge"] : ["minecraft"], [new(Collision ? "same.zip" : id + "-" + revision + (kind == "mod" ? ".jar" : ".zip"), "https://cdn.modrinth.com/qa/" + id + "/" + kind + "/" + revision, Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant(), bytes.Length, true)],
            id.StartsWith("dependent") ? [new("dependency", Unpinned ? null : "dependencyv" + (ConflictingDependency ? 2 : 1), "required")] : []);
    }
    public Task<ContentVersion[]> VersionsAsync(string id, string kind, CancellationToken ct) { ct.ThrowIfCancellationRequested(); if (Offline) throw new HttpRequestException("QA offline"); return Task.FromResult(new[] { Version(id, kind, Revision) }); }
    public Task<ContentVersion> VersionAsync(string id, CancellationToken ct) => Task.FromResult(Version(id[..id.LastIndexOf('v')], "mod", int.Parse(id[(id.LastIndexOf('v') + 1)..])));
    private sealed class Handler(ContentFixture fixture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); if (fixture.Offline || fixture.FailDownload) throw new HttpRequestException("QA download unavailable");
            var parts = request.RequestUri!.AbsolutePath.Split('/');
            var bytes = request.RequestUri.AbsolutePath.EndsWith("icon.png") ? Icon : fixture.Archive(parts[2], parts[3], int.Parse(parts[4]));
            if (fixture.Corrupt) bytes = new byte[bytes.Length];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = fixture.Slow ? new StreamContent(new SlowStream(bytes)) : new ByteArrayContent(bytes) });
        }
    }
    private sealed class SlowStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(500, cancellationToken); return await base.ReadAsync(buffer, cancellationToken); }
    }
}

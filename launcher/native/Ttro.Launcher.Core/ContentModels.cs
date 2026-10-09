namespace Ttro.Launcher.Core;

public sealed record SearchHit(string ProjectId, string Title, string Description, string Kind)
{
    public string Provider { get; init; } = "modrinth";
    public string? Author { get; init; }
    public string? IconUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string[] Gallery { get; init; } = [];
    public string[] Categories { get; init; } = [];
    public long? Downloads { get; init; }
    public DateTimeOffset? Updated { get; init; }
    public string? License { get; init; }
    public override string ToString() => Title;
}
public sealed record ContentQuery(string Query, string Kind, string Sort = "downloads", int Offset = 0, int Limit = 12, string? Category = null, string? Resolution = null);
public sealed record ContentPage(SearchHit[] Hits, int Offset, int Total);
public sealed record ContentDetails(SearchHit Project, string Body, string[] Versions);
public sealed record ContentDependency(string? ProjectId, string? VersionId, string Type);
public sealed record ContentFile(string Filename, string Url, string Sha512, long Size, bool Primary);
public sealed record ContentVersion(string ProjectId, string Id, string Number, string Name, DateTimeOffset Published, string VersionType, string[] GameVersions, string[] Loaders, ContentFile[] Files, ContentDependency[] Dependencies);
public sealed class InstalledContentRecord
{
    public string Provider { get; set; } = "modrinth";
    public string ProjectId { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string VersionNumber { get; set; } = "";
    public DateTimeOffset Published { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
    public string Sha512 { get; set; } = "";
    public string[] RequiredVersions { get; set; } = [];
}
public sealed record InstalledContent(string File, string Kind, bool Enabled, int? Priority, InstalledContentRecord? Managed);
public sealed record ContentProgress(string Stage, string Title, long? Received = null, long? Total = null);
public interface IContentProvider
{
    string Id { get; }
    Task<ContentPage> SearchAsync(ContentQuery query, CancellationToken ct);
    Task<ContentDetails> DetailsAsync(string projectId, string kind, CancellationToken ct);
    Task<ContentVersion[]> VersionsAsync(string projectId, string kind, CancellationToken ct);
    Task<ContentVersion> VersionAsync(string versionId, CancellationToken ct);
}

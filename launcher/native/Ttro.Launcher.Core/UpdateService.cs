using System.Text.Json.Nodes;

namespace Ttro.Launcher.Core;

public sealed record UpdateInfo(string Version, Uri Installer, string Sha256);
public static class UpdateService
{
    public const string CurrentVersion = "0.3.0-alpha.1";
    public static bool Newer(string candidate, string current)
    {
        var a = candidate.TrimStart('v').Split('-', 2); var b = current.TrimStart('v').Split('-', 2);
        if (!Version.TryParse(a[0], out var va) || !Version.TryParse(b[0], out var vb)) return false;
        if (va != vb) return va > vb;
        if (a.Length == 1) return b.Length > 1;
        if (b.Length == 1) return false;
        var aa = a[1].Split('.'); var bb = b[1].Split('.');
        if (aa.Length != 2 || bb.Length != 2 || aa[0] != bb[0]) return false;
        return int.TryParse(aa[1], out var ai) && int.TryParse(bb[1], out var bi) && ai > bi;
    }
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        using var http = new HttpClient(); http.DefaultRequestHeaders.UserAgent.ParseAdd("TtroClient/0.3.0-alpha.1");
        var releases = JsonNode.Parse(await http.GetStringAsync("https://api.github.com/repos/totoro0419/TtroClient/releases?per_page=30", ct))!.AsArray();
        foreach (var r in releases)
        {
            // This installation follows the reviewed alpha channel only. Never interpret a stable or unrelated prerelease as an alpha update.
            var tag = r!["tag_name"]!.GetValue<string>();
            if (r["draft"]!.GetValue<bool>() || r["prerelease"]?.GetValue<bool>() != true ||
                !System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+-alpha\.\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) continue;
            var version = tag[1..]; if (!Newer(version, CurrentVersion)) continue;
            var assets = r["assets"]!.AsArray();
            var setup = assets.FirstOrDefault(a => a!["name"]!.GetValue<string>() == "TtroClient-Setup.exe");
            var sums = assets.FirstOrDefault(a => a!["name"]!.GetValue<string>() == "checksums.txt");
            if (setup is null || sums is null) continue;
            var setupUri = TrustedAsset(setup["browser_download_url"]!.GetValue<string>()); var sumsUri = TrustedAsset(sums["browser_download_url"]!.GetValue<string>());
            var checksumText = await http.GetStringAsync(sumsUri, ct); var hash = ParseHash(checksumText, "TtroClient-Setup.exe");
            return new UpdateInfo(version, setupUri, hash);
        }
        return null;
    }
    public static Uri TrustedAsset(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/totoro0419/TtroClient/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("Update must come from the official Ttro Client release.");
        return uri;
    }
    public static string ParseHash(string text, string name)
    {
        var matches = text.Split('\n').Select(l => l.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length == 2 && p[1].TrimStart('*') == name).ToArray();
        if (matches.Length != 1 || matches[0][0].Length != 64 || !matches[0][0].All(Uri.IsHexDigit)) throw new InvalidDataException("Release checksum is missing or ambiguous."); return matches[0][0].ToLowerInvariant();
    }
    public static async Task<string> DownloadAsync(UpdateInfo update, string root, CancellationToken ct)
    {
        var dir = Path.Combine(root, "updates"); Directory.CreateDirectory(dir); var path = Path.Combine(dir, "TtroClient-Setup.exe");
        var temp = path + ".part"; try { await ContentService.DownloadAsync(TrustedAsset(update.Installer.ToString()), temp, ct); if (ProfileStore.Hash(temp) != update.Sha256) throw new InvalidDataException("Update failed SHA256 verification."); File.Move(temp, path, true); return path; } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

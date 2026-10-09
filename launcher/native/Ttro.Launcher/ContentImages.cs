using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using Ttro.Launcher.Core;

namespace Ttro.Launcher;

// One shared bounded cache; images are requested only for visible cards or opened details.
public sealed class ContentImages(string root, HttpClient? client = null)
{
    private readonly HttpClient http = client ?? ContentService.CreateClient();
    private readonly SemaphoreSlim gate = new(3, 3);
    private readonly Dictionary<string, (BitmapSource Image, long Bytes, long Used)> cache = [];
    private long clock;
    public async Task<BitmapSource?> LoadAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var uri = ContentService.TrustedDownload(url);
        if (cache.TryGetValue(url, out var hit)) { cache[url] = (hit.Image, hit.Bytes, ++clock); return hit.Image; }
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(url, out hit)) return hit.Image;
            var dir = Path.Combine(root, "preview-cache"); Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".image");
            byte[] bytes;
            if (File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromDays(7)) bytes = await File.ReadAllBytesAsync(file, ct);
            else
            {
                using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) return null;
                await using var input = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[16384]; int n;
                while ((n = await input.ReadAsync(buffer, ct)) > 0) { if (output.Length + n > 2 * 1024 * 1024) return null; output.Write(buffer, 0, n); }
                bytes = output.ToArray();
            }
            if (bytes.Length > 2 * 1024 * 1024) return null;
            var bitmap = await Task.Run(() => Decode(bytes), ct); ct.ThrowIfCancellationRequested();
            if (!File.Exists(file))
            {
                try { await File.WriteAllBytesAsync(file, bytes, ct); } catch (IOException) { /* A cache write is optional. */ }
            }
            var size = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
            while (cache.Count >= 48 || cache.Values.Sum(v => v.Bytes) + size > 16 * 1024 * 1024) cache.Remove(cache.MinBy(x => x.Value.Used).Key);
            cache[url] = (bitmap, size, ++clock);
            var files = new DirectoryInfo(dir).GetFiles("*.image").OrderByDescending(f => f.LastWriteTimeUtc).ToArray(); long total = 0;
            foreach (var f in files) { total += f.Length; if (total > 32 * 1024 * 1024 || files.Length > 128 && Array.IndexOf(files, f) >= 128) try { f.Delete(); } catch (IOException) { } }
            return bitmap;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException) { return null; }
        finally { gate.Release(); }
    }
    public static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 640; image.DecodePixelHeight = 360; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
}

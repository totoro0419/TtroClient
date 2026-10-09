using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Ttro.Launcher.Core;

public sealed class GameService(ProfileStore store, AuthService auth)
{
    public async Task PrepareAsync(IProgress<string> progress, CancellationToken ct, bool repair = false)
    {
        store.EnsureEditable(); var game = store.GameDirectory(store.Current); Directory.CreateDirectory(game);
        var launcher = new MinecraftLauncher(new MinecraftPath(game));
        var files = new Progress<InstallerProgressChangedEventArgs>(e => progress.Report($"Preparing {e.Name} ({e.ProgressedTasks}/{e.TotalTasks})"));
        progress.Report("Preparing Minecraft 1.8.9 and Java 8…");
        await launcher.InstallAsync("1.8.9", files, null, ct);
        var forge = new ForgeInstaller(launcher);
        var id = await forge.Install("1.8.9", "11.15.1.2318", new ForgeInstallOptions { FileProgress = files, CancellationToken = ct, SkipIfAlreadyInstalled = !repair });
        await launcher.InstallAsync(id, files, null, ct);
        InstallClient(game);
        var content = new ContentService(store);
        if (!content.HasProvider("patcher", true)) { progress.Report("Installing audited input/performance integration…"); await content.InstallProjectAsync("patcher", "mod", ct, "iNjGeSxM"); }
        if (!content.HasProvider("hypixel_mod_api", true)) { progress.Report("Installing official Hypixel Mod API…"); await content.InstallProjectAsync("hypixel-mod-api", "mod", ct, "VtDhN4ZW"); }
        var config = store.Settings(store.Current); store.WriteSettings(store.Current, config);
        ProfileStore.AtomicWrite(Path.Combine(game, "ttro-runtime.json"), new JsonObject { ["versionId"] = id }.ToJsonString());
        progress.Report("Ready. Minecraft 1.8.9 environment prepared.");
    }
    public static void InstallClient(string gameDirectory)
    {
        var payload = Path.Combine(AppContext.BaseDirectory, "payload");
        var manifestPath = Path.Combine(payload, "client-manifest.json");
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("Client payload is missing. Reinstall Ttro Client.");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!; var name = ProfileStore.SafeName(manifest["filename"]!.GetValue<string>());
        var source = Path.Combine(payload, name); if (ProfileStore.Hash(source) != manifest["sha256"]!.GetValue<string>()) throw new InvalidDataException("Client payload failed integrity verification. Reinstall Ttro Client.");
        var mods = Path.Combine(gameDirectory, "mods"); Directory.CreateDirectory(mods);
        var ownership = Path.Combine(gameDirectory, "ttro-client-owned.json");
        string? oldFileToArchive = null;
        if (File.Exists(ownership))
        {
            var old = JsonNode.Parse(File.ReadAllText(ownership))!; var oldName = ProfileStore.SafeName(old["filename"]!.GetValue<string>()); var oldFile = Path.Combine(mods, oldName);
            if (File.Exists(oldFile) && ProfileStore.Hash(oldFile) != old["sha256"]!.GetValue<string>()) throw new InvalidDataException("The managed client was modified. It has been preserved; remove it manually before repairing.");
            if (oldName != name && File.Exists(oldFile)) oldFileToArchive = oldFile;
        }
        var dest = Path.Combine(mods, name);
        if (File.Exists(dest) && ProfileStore.Hash(dest) != manifest["sha256"]!.GetValue<string>()) throw new IOException("An unrecognized client file has been preserved. Remove or rename it before repairing.");
        // Check all collisions and stage the replacement before touching the previous managed JAR.
        var tmp = dest + ".tmp"; string? archive = null; var created = !File.Exists(dest);
        try
        {
            File.Copy(source, tmp, true);
            if (oldFileToArchive is not null)
            {
                archive = Path.Combine(gameDirectory, "backups", Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(oldFileToArchive));
                Directory.CreateDirectory(Path.GetDirectoryName(archive)!); File.Copy(oldFileToArchive, archive);
            }
            File.Move(tmp, dest, true);
            try { if (oldFileToArchive is not null) File.Delete(oldFileToArchive); ProfileStore.AtomicWrite(ownership, manifest.ToJsonString()); }
            catch { if (oldFileToArchive is not null && archive is not null && !File.Exists(oldFileToArchive)) File.Copy(archive, oldFileToArchive); if (created) File.Delete(dest); throw; }
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    public async Task PlayAsync(IProgress<string> progress, CancellationToken ct)
    {
        if (store.GameRunning) throw new InvalidOperationException("Minecraft is already running.");
        var session = await auth.RefreshAsync(ct); await PrepareAsync(progress, ct);
        var game = store.GameDirectory(store.Current); var id = JsonNode.Parse(File.ReadAllText(Path.Combine(game, "ttro-runtime.json")))!["versionId"]!.GetValue<string>();
        var launcher = new MinecraftLauncher(new MinecraftPath(game));
        ProfileStore.Validate(store.Current);
        var process = await launcher.BuildProcessAsync(id, new MLaunchOption { Session = session, MaximumRamMb = store.Current.RamMb, MinimumRamMb = Math.Min(512, store.Current.RamMb), GameLauncherName = "Ttro Client", GameLauncherVersion = "0.3.0-alpha.1", ScreenWidth = 1280, ScreenHeight = 720, ExtraJvmArguments = store.Current.JvmArguments.Select(a => new MArgument(a)).ToArray() }, ct);
        var logDir = Path.Combine(store.Root, "logs"); Directory.CreateDirectory(logDir);
        var log = Path.Combine(logDir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log");
        process.StartInfo.RedirectStandardOutput = true; process.StartInfo.RedirectStandardError = true; process.StartInfo.UseShellExecute = false;
        store.GameRunning = true;
        try
        {
            progress.Report("Minecraft is running. Client settings are available with Right Ctrl.");
            process.Start();
            var writeLock = new object();
            void Write(string? line) { if (line is null) return; var safe = string.IsNullOrEmpty(session.AccessToken) ? line : line.Replace(session.AccessToken, "[REDACTED]", StringComparison.Ordinal); lock (writeLock) File.AppendAllText(log, safe + "\n"); }
            process.OutputDataReceived += (_, e) => Write(e.Data); process.ErrorDataReceived += (_, e) => Write(e.Data); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            // Cancel prepares/downloads only; it never kills a game or corrupts a world.
            await process.WaitForExitAsync(); process.WaitForExit();
            progress.Report(process.ExitCode == 0 ? "Minecraft closed." : $"Minecraft exited with code {process.ExitCode}. Open Logs for details.");
        }
        finally { store.GameRunning = false; process.Dispose(); }
    }
}

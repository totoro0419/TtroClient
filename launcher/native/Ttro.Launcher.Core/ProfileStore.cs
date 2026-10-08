using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace Ttro.Launcher.Core;

public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Competitive PvP";
    public string MinecraftVersion { get; set; } = "1.8.9";
    public int RamMb { get; set; } = 2048;
    public string Performance { get; set; } = "Balanced";
    public string[] JvmArguments { get; set; } = [];
    public List<string> Packs { get; set; } = [];
    public Dictionary<string, string> ManagedMods { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class LauncherState
{
    public int Schema { get; set; } = 1;
    public string Selected { get; set; } = "";
    public List<Profile> Profiles { get; set; } = [];
}

public sealed class ProfileStore
{
    public string Root { get; }
    private string StatePath => Path.Combine(Root, "launcher.json");
    public LauncherState State { get; private set; }
    public JsonArray Catalog { get; }
    public Profile Current => State.Profiles.Single(p => p.Id == State.Selected);
    public bool GameRunning { get; set; }
    public ProfileStore(string root, string catalogPath)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        Catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsArray();
        State = File.Exists(StatePath)
            ? JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(StatePath)) ?? throw new InvalidDataException("Invalid profile store; original file has been preserved.")
            : new LauncherState();
        if (State.Schema != 1) throw new InvalidDataException("Unsupported profile version; original file has been preserved.");
        if (State.Profiles.Count == 0) { var p = new Profile(); State.Profiles.Add(p); State.Selected = p.Id; }
        foreach (var p in State.Profiles) Validate(p);
        if (State.Profiles.Select(p => p.Id).Distinct().Count() != State.Profiles.Count || !State.Profiles.Any(p => p.Id == State.Selected)) throw new InvalidDataException("Invalid profile selection.");
        Save();
    }
    public string GameDirectory(Profile profile) { Validate(profile); return Path.Combine(Root, "profiles", profile.Id); }
    public static void Validate(Profile profile)
    {
        if (profile.Id.Length != 32 || !profile.Id.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid profile identity.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80) throw new InvalidDataException("Profile name must be 1–80 characters.");
        if (profile.MinecraftVersion != "1.8.9") throw new InvalidDataException("Ttro Client requires Minecraft 1.8.9.");
        if (profile.RamMb < 1024 || profile.RamMb > 32768) throw new InvalidDataException("RAM must be 1024–32768 MiB.");
        // This field is a vector, never shell text. Restrict advanced flags to GC/runtime tuning.
        foreach (var a in profile.JvmArguments)
            if (a.Any(char.IsWhiteSpace) || !(a.StartsWith("-XX:") || a.StartsWith("-Dfile.encoding=") || a.StartsWith("-Djava.net.preferIPv4Stack=")))
                throw new InvalidDataException("JVM options must be individual GC/runtime flags; authentication and classpath overrides are not accepted.");
        foreach (var n in profile.Packs.Concat(profile.ManagedMods.Keys)) SafeName(n);
    }
    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.Contains('/') || name.Contains('\\') || name.Contains(':') || name.Contains("..") || name.Any(char.IsControl))
            throw new InvalidDataException("Unsafe content filename.");
        return name;
    }
    public void EnsureEditable() { if (GameRunning) throw new InvalidOperationException("Minecraft is running. Use the in-game settings, or close Minecraft before editing this profile."); }
    public void Save()
    {
        foreach (var p in State.Profiles) Validate(p);
        AtomicWrite(StatePath, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
    }
    public Profile Add(string name)
    {
        EnsureEditable(); var p = new Profile { Name = name.Trim() }; Validate(p);
        State.Profiles.Add(p); State.Selected = p.Id; Save(); return p;
    }
    public void Select(Profile p) { EnsureEditable(); State.Selected = p.Id; Save(); }
    public JsonObject Settings(Profile p)
    {
        var path = Path.Combine(GameDirectory(p), "config", "ttro-client.json");
        var settings = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject { ["schema"] = 2, ["modules"] = new JsonObject(), ["hud"] = new JsonObject { ["layout"] = "clusters", ["x"] = 12, ["y"] = 12, ["scale"] = 1 } };
        if (settings["schema"]?.GetValue<int>() != 2 || settings["modules"] is not JsonObject) throw new InvalidDataException("Client settings are invalid; original file has been preserved.");
        foreach (var item in Catalog)
        {
            var m = item!.AsObject(); var id = m["id"]!.GetValue<string>();
            var ms = settings["modules"]!.AsObject();
            if (ms[id] is null) ms[id] = new JsonObject { ["enabled"] = m["status"]!.GetValue<string>() != "candidate" && m["default"]!.GetValue<bool>() };
            foreach (var pair in m["settings"]!.AsObject())
                if (ms[id]![pair.Key] is null) ms[id]![pair.Key] = pair.Value!["default"]!.DeepClone();
        }
        return settings;
    }
    public void SetValue(string id, string key, JsonNode value)
    {
        EnsureEditable(); var meta = Catalog.Select(m => m!.AsObject()).Single(m => m["id"]!.GetValue<string>() == id);
        if (key == "enabled" && value.GetValue<bool>() && meta["status"]!.GetValue<string>() == "candidate") throw new InvalidOperationException("This module is not implemented.");
        if (key != "enabled")
        {
            var def = meta["settings"]![key] ?? throw new InvalidDataException("Unknown setting.");
            switch (def["type"]!.GetValue<string>())
            {
                case "boolean": _ = value.GetValue<bool>(); break;
                case "number": var n = value.GetValue<double>(); if (!double.IsFinite(n) || n < def["min"]!.GetValue<double>() || n > def["max"]!.GetValue<double>()) throw new InvalidDataException("Value is outside the supported range."); break;
                case "select": if (!def["options"]!.AsArray().Any(x => x!.GetValue<string>() == value.GetValue<string>())) throw new InvalidDataException("Unsupported choice."); break;
            }
        }
        var settings = Settings(Current); settings["modules"]![id]![key] = value.DeepClone();
        WriteSettings(Current, settings);
    }
    public void WriteSettings(Profile p, JsonObject settings)
    {
        EnsureEditable(); AtomicWrite(Path.Combine(GameDirectory(p), "config", "ttro-client.json"), settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { var bytes = System.Text.Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush(true); } File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public string Backup(string file)
    {
        var dir = Path.Combine(Root, "backups"); Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(file));
        if (File.Exists(file)) File.Copy(file, dest); return dest;
    }
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

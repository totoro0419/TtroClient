using Ttro.Launcher.Core;
using System.IO.Compression;
using System.Text.Json.Nodes;

if (args.Length == 2 && args[0] == "--export-game-packs") { await GamePackQa.Export(Path.Combine(AppContext.BaseDirectory, "modules.json"), Path.GetFullPath(args[1])); return; }
var root = Path.Combine(Path.GetTempPath(), "ttro-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root); int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is InvalidDataException or InvalidOperationException or IOException) { Check(true, name); return; } throw new Exception("Accepted: " + name); }
string Zip(string name, Dictionary<string, string> entries) { var path = Path.Combine(root, name); using var z = ZipFile.Open(path, ZipArchiveMode.Create); foreach (var e in entries) { using var w = new StreamWriter(z.CreateEntry(e.Key).Open()); w.Write(e.Value); } return path; }
try
{
 var state = Path.Combine(root, "state"); var catalog = Path.Combine(AppContext.BaseDirectory, "modules.json");
 var store = new ProfileStore(state, catalog); var first = store.Current.Id;
 Check(store.Current.MinecraftVersion == "1.8.9", "target fixed to 1.8.9");
 store.Add("BedWars"); store.Current.RamMb = 4096; store.Save();
 var again = new ProfileStore(state, catalog); Check(again.State.Profiles.Count == 2 && again.Current.RamMb == 4096 && again.Current.Id != first, "profile persistence and selection");
 Reject(() => ProfileStore.Validate(new Profile { MinecraftVersion = "1.21.1" }), "reject incompatible target");
 Reject(() => ProfileStore.Validate(new Profile { JvmArguments = ["-javaagent:untrusted.jar"] }), "reject classpath/agent override");
 Reject(() => ProfileStore.SafeName("../world"), "reject traversal");
 store.SetValue("crosshair", "enabled", JsonValue.Create(true)!);
 Check(store.Settings(store.Current)["modules"]!["crosshair"]!["enabled"]!.GetValue<bool>(), "module edits persist");
 var candidate = store.Catalog.First(m => m!["status"]!.GetValue<string>() == "candidate")!["id"]!.GetValue<string>();
 Reject(() => store.SetValue(candidate, "enabled", JsonValue.Create(true)!), "candidate cannot masquerade as working");
 store.GameRunning = true; Reject(() => store.Add("unsafe"), "running game blocks profile edits"); store.GameRunning = false;
 var content = new ContentService(store);
 var a = Zip("a.zip", new() { ["pack.mcmeta"] = "{\"pack\":{\"pack_format\":1,\"description\":\"A\"}}" });
 var b = Zip("b.zip", new() { ["pack.mcmeta"] = "{\"pack\":{\"pack_format\":1,\"description\":\"B\"}}" });
 content.Import(a, "resourcepack"); content.Import(b, "resourcepack");
 Check(store.Current.Packs.SequenceEqual(new[] { "b.zip", "a.zip" }), "pack UI highest first");
 var options = Path.Combine(store.GameDirectory(store.Current), "options.txt");
 Check(File.ReadAllText(options).Contains("[\"a.zip\",\"b.zip\"]"), "pack priority serialized for vanilla");
 content.MovePack("b.zip", 1); Check(store.Current.Packs[0] == "a.zip", "pack reorder");
 Reject(() => content.Import(a, "resourcepack"), "import preserves collision");
 content.Toggle("a.zip", "resourcepack"); Check(!store.Current.Packs.Contains("a.zip"), "pack disable");
 content.Delete("a.zip", "resourcepack"); Check(Directory.GetFiles(Path.Combine(state, "backups")).Length == 1, "delete creates backup");
 var malicious = Zip("traversal.zip", new() { ["../bad"] = "x", ["pack.mcmeta"] = "{}" }); Reject(() => content.Import(malicious, "resourcepack"), "zip traversal");
 var fabric = Zip("fabric.jar", new() { ["fabric.mod.json"] = "{}" }); Reject(() => content.Import(fabric, "mod"), "reject Fabric artifact");
 var duplicate = Zip("mouse.jar", new() { ["mcmod.info"] = "[{\"modid\":\"mousetweaks\",\"mcversion\":\"1.8.9\"}]" }); Reject(() => content.Import(duplicate, "mod"), "reject duplicate Mouse Tweaks");
 Check(UpdateService.Newer("v0.3.0-alpha.2", "0.3.0-alpha.1") && !UpdateService.Newer("0.3.0-alpha.1", "0.3.0"), "alpha update ordering");
 Reject(() => UpdateService.TrustedAsset("https://github.com/other/client/releases/download/v1/setup.exe"), "update origin bound to canonical repository");
 Reject(() => UpdateService.ParseHash(new string('a', 64) + "  TtroClient-Setup.exe\n" + new string('b', 64) + "  TtroClient-Setup.exe", "TtroClient-Setup.exe"), "ambiguous checksum rejected");
 var future = Path.Combine(root, "future"); Directory.CreateDirectory(future); var file = Path.Combine(future, "launcher.json"); var original = "{\"Schema\":99}"; File.WriteAllText(file, original);
 Reject(() => new ProfileStore(future, catalog), "unknown schema fails closed"); Check(File.ReadAllText(file) == original, "unknown schema preserves file");
 await ContentQa.Run(catalog);
 if (args.Contains("--live-content")) await LiveContentQa.Run(catalog);
 Console.WriteLine($"{checks} original core checks passed.");
}
finally { Directory.Delete(root, true); }

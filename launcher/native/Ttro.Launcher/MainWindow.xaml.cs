using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Ttro.Launcher.Core;
using Microsoft.Win32;

namespace Ttro.Launcher;

public partial class MainWindow : Window
{
 private ProfileStore? store; private ContentService? content; private AuthService? auth; private GameService? game;
 private CancellationTokenSource? operation; private UpdateInfo? update; private bool ready;
 private sealed class ModuleEntry(string id, string name, string status) : System.ComponentModel.INotifyPropertyChanged
 {
  public string Id { get; } = id; public string Name { get; } = name; private string state = status;
  public string Status { get => state; set { state = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Display))); } }
  public string Display => Name + " · " + Status;
  public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
  public override string ToString() => Display;
 }
 public MainWindow() { InitializeComponent(); ModuleList.DisplayMemberPath = "Display"; PreviewKeyDown += Shortcut; }
 private void Shortcut(object sender, KeyEventArgs e)
 {
  if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.K && operation is null) { Workspace.SelectedIndex = 1; ModuleSearch.Focus(); ModuleSearch.SelectAll(); e.Handled = true; }
  else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.Enter && operation is null) { PlayClick(this, new RoutedEventArgs()); e.Handled = true; }
 }
 private void WindowLoaded(object sender, RoutedEventArgs e)
 {
  try
  {
   var root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TtroClient189", "native");
   store = new ProfileStore(root, System.IO.Path.Combine(AppContext.BaseDirectory, "modules.json"));
   content = new ContentService(store); auth = new AuthService(root, System.IO.Path.Combine(AppContext.BaseDirectory, "launcher-settings.json")); game = new GameService(store, auth);
   ready = true; RefreshProfiles(); RefreshModules(); RefreshContent(); Profiles.Focus();
   if (!auth.Configured) Status.Text = "Development alpha: Microsoft sign-in is not configured for this build. Settings and local content management are available.";
  }
  catch (Exception ex) { Status.Text = "Startup failed: " + ex.Message; PlayButton.IsEnabled = false; LoginButton.IsEnabled = false; Workspace.IsEnabled = false; }
 }
 private async Task RunAsync(Func<CancellationToken, Task> task)
 {
  if (!ready || operation is not null) return;
  operation = new CancellationTokenSource(); Progress.Visibility = Visibility.Visible; CancelButton.Visibility = Visibility.Visible;
  Workspace.IsEnabled = false; Profiles.IsEnabled = false; LoginButton.IsEnabled = false;
  try { await task(operation.Token); }
  catch (OperationCanceledException) { Status.Text = "Preparation cancelled. Your profiles and worlds have been preserved."; }
  catch (Exception ex) { Status.Text = "Unable to complete this action: " + ex.Message + " Retry the action after correcting the issue."; }
  finally { operation.Dispose(); operation = null; Progress.Visibility = Visibility.Collapsed; CancelButton.Visibility = Visibility.Collapsed; Workspace.IsEnabled = true; Profiles.IsEnabled = true; LoginButton.IsEnabled = true; RefreshModules(); RefreshContent(); }
 }
 private void Local(Action action)
 {
  if (!ready) return; try { store!.EnsureEditable(); action(); } catch (Exception ex) { Status.Text = ex.Message; }
 }
 private IProgress<string> Feedback() => new Progress<string>(s => { Status.Text = s; if (store!.GameRunning) CancelButton.Visibility = Visibility.Collapsed; });
 private async void LoginClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { Status.Text = "Opening Microsoft sign-in in your browser…"; var session = await auth!.LoginAsync(ct); Account.Text = "Signed in as " + session.Username; Status.Text = "Signed in. Select a profile and press PLAY."; });
 private async void PlayClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { await game!.PlayAsync(Feedback(), ct); if (auth!.Session is not null) Account.Text = "Signed in as " + auth.Session.Username; });
 private async void RepairClick(object sender, RoutedEventArgs e) => await RunAsync(ct => game!.PrepareAsync(Feedback(), ct, true));
 private void CancelClick(object sender, RoutedEventArgs e) => operation?.Cancel();
 private void WindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
 {
  if (operation is not null && store?.GameRunning != true) { e.Cancel = true; operation.Cancel(); Status.Text = "Cancelling preparation. Close the launcher again after it stops."; }
 }
 private void RefreshProfiles()
 {
  ready = false; Profiles.ItemsSource = store!.State.Profiles.ToArray(); Profiles.SelectedItem = store.Current;
  Ram.Text = store.Current.RamMb.ToString(); Jvm.Text = string.Join("\n", store.Current.JvmArguments);
  foreach (ComboBoxItem item in Performance.Items) if ((string)item.Content == store.Current.Performance) Performance.SelectedItem = item;
  HudLayout.SelectedIndex = store.Settings(store.Current)["hud"]?["layout"]?.GetValue<string>() == "individual" ? 1 : 0;
  ready = true; DrawCrosshair();
 }
 private void ProfileChanged(object sender, SelectionChangedEventArgs e)
 {
  if (!ready || Profiles.SelectedItem is not Profile p) return;
  Local(() => { store!.Select(p); RefreshProfiles(); RefreshModules(); RefreshContent(); Status.Text = "Profile selected: " + p.Name; });
 }
 private void ProfileAddClick(object sender, RoutedEventArgs e) => Local(() => { store!.Add(ProfileName.Text); RefreshProfiles(); RefreshModules(); RefreshContent(); Status.Text = "Profile created."; });
 private void SaveProfileClick(object sender, RoutedEventArgs e) => Local(() =>
 {
  if (!int.TryParse(Ram.Text, out var ram)) throw new InvalidDataException("Enter RAM as a whole number of MiB.");
  var current = store!.Current; var oldRam = current.RamMb; var oldArgs = current.JvmArguments; var oldPreset = current.Performance;
  try
  {
   current.RamMb = ram; current.JvmArguments = Jvm.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
   current.Performance = ((ComboBoxItem)Performance.SelectedItem).Content.ToString()!; ProfileStore.Validate(current); store.Save();
   var path = System.IO.Path.Combine(store.GameDirectory(current), "options.txt"); var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
   var presets = current.Performance switch { "Competitive" => new Dictionary<string, string> { ["renderDistance"] = "6", ["fancyGraphics"] = "false", ["particles"] = "2", ["enableVsync"] = "false" }, "Quality" => new Dictionary<string, string> { ["renderDistance"] = "12", ["fancyGraphics"] = "true", ["particles"] = "0" }, _ => new Dictionary<string, string> { ["renderDistance"] = "8", ["fancyGraphics"] = "false", ["particles"] = "1" } };
   foreach (var pair in presets) { lines.RemoveAll(l => l.StartsWith(pair.Key + ":")); lines.Add(pair.Key + ":" + pair.Value); }
   store.Backup(path); ProfileStore.AtomicWrite(path, string.Join("\n", lines) + "\n"); Status.Text = "Profile saved. Performance preset applies at the next launch; it is not a measured FPS guarantee.";
  }
  catch { current.RamMb = oldRam; current.JvmArguments = oldArgs; current.Performance = oldPreset; store.Save(); throw; }
 });
 private void SearchChanged(object sender, TextChangedEventArgs e) { if (ready) RefreshModules(); }
 private void RefreshModules()
 {
  if (!ready) return; var selected = (ModuleList.SelectedItem as ModuleEntry)?.Id;
  var settings = store!.Settings(store.Current)["modules"]!; var query = ModuleSearch.Text;
  var list = store.Catalog.Select(n => n!.AsObject()).Where(m => m["name"]!.GetValue<string>().Contains(query, StringComparison.OrdinalIgnoreCase)).Select(m =>
  {
   var id = m["id"]!.GetValue<string>(); var status = m["status"]!.GetValue<string>();
   if (status == "candidate") return new ModuleEntry(id, m["name"]!.GetValue<string>(), "Not implemented");
   if (m["requires"] is not null && !content!.HasProvider(m["requires"]!.GetValue<string>())) return new ModuleEntry(id, m["name"]!.GetValue<string>(), "Provider missing");
   return new ModuleEntry(id, m["name"]!.GetValue<string>(), m["control"]?.GetValue<string>() == "foundation" ? "Foundation active" : settings[id]!["enabled"]!.GetValue<bool>() ? "ON" : "OFF");
  }).ToArray();
  ModuleList.ItemsSource = list; ModuleList.SelectedItem = list.FirstOrDefault(m => m.Id == selected) ?? list.FirstOrDefault();
  if (list.Length == 0) { ModuleContext.Children.Clear(); ModuleContext.Children.Add(new TextBlock { Text = "No matching modules. Try a shorter search.", TextWrapping = TextWrapping.Wrap }); }
 }
 private void ModuleChanged(object sender, SelectionChangedEventArgs e)
 {
  if (!ready || ModuleList.SelectedItem is not ModuleEntry entry) return; ModuleContext.Children.Clear();
  var meta = store!.Catalog.Select(n => n!.AsObject()).Single(m => m["id"]!.GetValue<string>() == entry.Id); var values = store.Settings(store.Current)["modules"]![entry.Id]!;
  var available = meta["status"]!.GetValue<string>() != "candidate" && (meta["requires"] is null || content!.HasProvider(meta["requires"]!.GetValue<string>()));
  void Text(string text, bool title = false) => ModuleContext.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = title ? 23 : 15, FontWeight = title ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 4, 0, 12) });
  Text(entry.Name, true); Text(entry.Status + " · " + meta["group"]!.GetValue<string>());
  Text("Server category: " + meta["safety"]!.GetValue<string>() + ". The server profile may restrict this feature in game. No ban exemption is implied.");
  if (meta["note"] is not null && !string.IsNullOrEmpty(meta["note"]!.GetValue<string>())) Text(meta["note"]!.GetValue<string>());
  if (meta["control"]?.GetValue<string>() == "foundation") Text("This is a permanent input fix supplied by PolyPatcher. It is not an optional toggle.");
  else
  {
   var toggle = new CheckBox { Content = "Enable " + entry.Name, IsChecked = values["enabled"]!.GetValue<bool>(), IsEnabled = available };
   RoutedEventHandler saveToggle = (_, _) => Local(() => { store.SetValue(entry.Id, "enabled", JsonValue.Create(toggle.IsChecked == true)!); entry.Status = toggle.IsChecked == true ? "ON" : "OFF"; Status.Text = "Saved " + entry.Name + ". Changes apply on the next launch."; DrawCrosshair(); });
   toggle.Checked += saveToggle; toggle.Unchecked += saveToggle; ModuleContext.Children.Add(toggle);
  }
  foreach (var pair in meta["settings"]!.AsObject())
  {
   var key = pair.Key; var def = pair.Value!; var label = new Label { Content = key }; ModuleContext.Children.Add(label);
   if (def["type"]!.GetValue<string>() == "boolean")
   {
    var box = new CheckBox { Content = key, IsChecked = values[key]!.GetValue<bool>(), IsEnabled = available };
    RoutedEventHandler saveBoolean = (_, _) => Local(() => store.SetValue(entry.Id, key, JsonValue.Create(box.IsChecked == true)!));
    box.Checked += saveBoolean; box.Unchecked += saveBoolean; label.Target = box; ModuleContext.Children.Add(box);
   }
   else if (def["type"]!.GetValue<string>() == "select")
   {
    var select = new ComboBox { ItemsSource = def["options"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray(), SelectedItem = values[key]!.GetValue<string>(), IsEnabled = available };
    select.SelectionChanged += (_, _) => { if (select.SelectedItem is string value) Local(() => { store.SetValue(entry.Id, key, JsonValue.Create(value)!); DrawCrosshair(); }); }; label.Target = select; ModuleContext.Children.Add(select);
   }
   else
   {
    var number = new TextBox { Text = values[key]!.ToString(), IsEnabled = available, ToolTip = def["min"] + " – " + def["max"] };
    void SaveNumber() => Local(() => { if (!double.TryParse(number.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)) throw new InvalidDataException("Enter a number using a decimal point."); store.SetValue(entry.Id, key, JsonValue.Create(n)!); DrawCrosshair(); });
    number.LostKeyboardFocus += (_, _) => SaveNumber(); number.KeyDown += (_, k) => { if (k.Key == Key.Enter) { SaveNumber(); k.Handled = true; } }; label.Target = number; ModuleContext.Children.Add(number);
   }
  }
 }
 private void WorkspaceChanged(object sender, SelectionChangedEventArgs e) { if (ready && e.Source == Workspace) { DrawCrosshair(); RefreshContent(); } }
 private void HudLayoutChanged(object sender, SelectionChangedEventArgs e)
 {
  if (!ready || HudLayout.SelectedItem is not ComboBoxItem selected) return;
  Local(() => { var settings = store!.Settings(store.Current); settings["hud"]!["layout"] = selected.Tag.ToString(); store.WriteSettings(store.Current, settings); Status.Text = "HUD layout saved. Use the in-game HUD editor for placement."; });
 }
 private void DrawCrosshair()
 {
  if (!ready) return; CrosshairPreview.Children.Clear(); var v = store!.Settings(store.Current)["modules"]!["crosshair"]!;
  var size = v["size"]!.GetValue<double>(); var gap = v["gap"]!.GetValue<double>(); var shape = v["shape"]!.GetValue<string>();
  if (!v["enabled"]!.GetValue<bool>()) { CrosshairPreview.Children.Add(new TextBlock { Text = "Crosshair disabled", Margin = new Thickness(20) }); return; }
  void Rect(double x, double y, double w, double h) { var r = new Rectangle { Width = w, Height = h, Fill = SystemColors.WindowTextBrush }; Canvas.SetLeft(r, x); Canvas.SetTop(r, y); CrosshairPreview.Children.Add(r); }
  if (shape == "dot") Rect(109, 79, 3, 3); else { Rect(110-gap-size,79,size,2); Rect(110+gap,79,size,2); Rect(109,80+gap,2,size); if (shape != "t") Rect(109,80-gap-size,2,size); }
 }
 private string Kind => (ContentKind?.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "resourcepack";
 private string SelectedFile() => ContentList.SelectedItem as string ?? throw new InvalidOperationException("Select installed content first.");
 private void ContentKindChanged(object sender, SelectionChangedEventArgs e) { if (ready) RefreshContent(); }
 private void ContentRefreshClick(object sender, RoutedEventArgs e) => RefreshContent();
 private void RefreshContent()
 {
  if (!ready) return; var files = content!.Files(Kind); ContentList.ItemsSource = Kind == "resourcepack" ? store!.Current.Packs.Concat(files.Where(f => !store.Current.Packs.Contains(f))).ToArray() : files;
  ContentEmpty.Visibility = files.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
 }
 private void ImportClick(object sender, RoutedEventArgs e) => Local(() => { var dialog = new OpenFileDialog { Filter = Kind == "mod" ? "Forge 1.8.9 mod|*.jar" : "Minecraft 1.8.9 resource pack|*.zip" }; if (dialog.ShowDialog(this) == true) { content!.Import(dialog.FileName, Kind); RefreshContent(); RefreshModules(); Status.Text = "Content imported. Applies on the next launch."; } });
 private void ContentToggleClick(object sender, RoutedEventArgs e) => Local(() => { content!.Toggle(SelectedFile(), Kind); RefreshContent(); RefreshModules(); Status.Text = "Content state saved. Applies on the next launch."; });
 private void PackUpClick(object sender, RoutedEventArgs e) => Local(() => { if (Kind != "resourcepack") throw new InvalidOperationException("Only packs have priority order."); content!.MovePack(SelectedFile(), -1); RefreshContent(); });
 private void PackDownClick(object sender, RoutedEventArgs e) => Local(() => { if (Kind != "resourcepack") throw new InvalidOperationException("Only packs have priority order."); content!.MovePack(SelectedFile(), 1); RefreshContent(); });
 private void DeleteClick(object sender, RoutedEventArgs e) => Local(() => { var name = SelectedFile(); if (MessageBox.Show(this, "Delete " + name + "? A backup will be retained.", "Delete content", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) { content!.Delete(name, Kind); RefreshContent(); RefreshModules(); Status.Text = "Content deleted; backup retained."; } });
 private void PreviewClick(object sender, RoutedEventArgs e) => Local(() =>
 {
  var file = System.IO.Path.Combine(content!.DirectoryFor(Kind), SelectedFile()); using var zip = ZipFile.OpenRead(file); var icon = zip.GetEntry("pack.png");
  var panel = new StackPanel { Margin = new Thickness(20) }; panel.Children.Add(new TextBlock { Text = System.IO.Path.GetFileName(file), TextWrapping = TextWrapping.Wrap, FontSize = 20 });
  if (icon is not null && icon.Length <= 4*1024*1024) { using var stream = icon.Open(); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320; image.StreamSource = stream; image.EndInit(); image.Freeze(); panel.Children.Add(new Image { Source = image, MaxHeight = 320, Margin = new Thickness(0, 16, 0, 0) }); }
  else panel.Children.Add(new TextBlock { Text = "This archive contains no supported preview image.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,16,0,0) });
  var dialog = new Window { Title = "Content preview", Owner = this, Width = 420, Height = 440, Content = panel, Background = SystemColors.WindowBrush }; dialog.PreviewKeyDown += (_, k) => { if (k.Key == Key.Escape) dialog.Close(); }; dialog.ShowDialog();
 });
 private async void ContentQueryKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await Search(); } }
 private async void ContentSearchClick(object sender, RoutedEventArgs e) => await Search();
 private async Task Search() => await RunAsync(async ct => { Status.Text = "Searching compatible 1.8.9 content…"; SearchResults.ItemsSource = await content!.SearchAsync(ContentQuery.Text, Kind, ct); Status.Text = SearchResults.Items.Count == 0 ? "No results. Try another search." : "Select a result and install it. Compatible dependencies are resolved before changes are applied."; });
 private async void ContentInstallClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { if (SearchResults.SelectedItem is not SearchHit hit) throw new InvalidOperationException("Select a search result first."); Status.Text = "Downloading and verifying " + hit.Title + "…"; await content!.InstallProjectAsync(hit.ProjectId, hit.Kind, ct); Status.Text = "Content installed and verified. Applies on next launch."; });
 private async void IntegrationsClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { Status.Text = "Installing audited PolyPatcher 1.10.4 and Hypixel Mod API 1.0.2 from Modrinth…"; await content!.InstallProjectAsync("patcher", "mod", ct, "iNjGeSxM"); await content.InstallProjectAsync("hypixel-mod-api", "mod", ct, "VtDhN4ZW"); Status.Text = "Recommended integrations installed. Restart Minecraft to load them."; });
 private async void UpdateCheckClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { Status.Text = "Checking official releases…"; update = await UpdateService.CheckAsync(ct); UpdateButton.IsEnabled = update is not null; Status.Text = update is null ? "No newer installable release is available." : "Ttro Client " + update.Version + " is available. Update & Restart downloads and verifies the official installer."; });
 private async void UpdateClick(object sender, RoutedEventArgs e) => await RunAsync(async ct => { if (update is null) throw new InvalidOperationException("Check for updates first."); var installer = await UpdateService.DownloadAsync(update, store!.Root, ct); Process.Start(new ProcessStartInfo(installer, "/S /RESTART") { UseShellExecute = true }); Application.Current.Shutdown(); });
 private async void SignOutClick(object sender, RoutedEventArgs e) => await RunAsync(async _ => { await auth!.SignOutAsync(); Account.Text = "Signed out."; Status.Text = "Microsoft session cleared."; });
 private void LogsClick(object sender, RoutedEventArgs e) { if (!ready) return; var logs = System.IO.Path.Combine(store!.Root, "logs"); Directory.CreateDirectory(logs); Process.Start(new ProcessStartInfo("explorer.exe", logs) { UseShellExecute = true }); }
 private void GitHubClick(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/totoro0419/TtroClient") { UseShellExecute = true });
}

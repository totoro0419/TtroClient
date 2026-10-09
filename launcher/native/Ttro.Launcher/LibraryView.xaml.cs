using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Ttro.Launcher.Core;

namespace Ttro.Launcher;

public partial class LibraryView : UserControl
{
    private ProfileStore? store;
    private ContentService? content;
    private ContentImages? images;
    private CancellationTokenSource? browse, debounce, install, imageRequests;
    private readonly Dictionary<string, ContentVersion> updates = [];
    private readonly List<SearchHit> favorites = [];
    private readonly List<(Image Image, string? Url, TextBlock Fallback)> previews = [];
    private readonly HashSet<Image> requested = [];
    private readonly Dictionary<string, (Button Button, TextBlock State, string Title)> actions = [];
    private int offset, total, requestId;
    private bool initialized, active;
    private string? renderedScope;
    public bool IsMutating => install is not null;
    public event Action<bool>? BusyChanged;
    public event Action<string>? Changed;
    private string Kind => (ContentKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "resourcepack";
    private bool Discover => DiscoverButton.IsChecked == true;
    private string FavoritesPath => Path.Combine(store!.Root, "content-favorites.json");
    public LibraryView() => InitializeComponent();
    public void Initialize(ProfileStore profileStore, ContentService service, ContentImages? imageCache = null)
    {
        store = profileStore; content = service; images = imageCache ?? new ContentImages(store.Root);
        if (File.Exists(FavoritesPath))
        {
            try { favorites.AddRange((JsonSerializer.Deserialize<SearchHit[]>(File.ReadAllText(FavoritesPath)) ?? []).Where(h => h.Provider == "modrinth").Take(200)); }
            catch (JsonException) { LibraryStatus.Text = "Favorites could not be read. The original file has been preserved."; }
        }
        initialized = true; RefreshProfile();
    }
    public void RefreshProfile()
    {
        if (!initialized) return;
        browse?.Cancel(); debounce?.Cancel(); requestId++; updates.Clear();
        renderedScope = null; ProfileScope.Text = "Profile: " + store!.Current.Name;
        RefreshInstalled(); UpdateCardStates();
        if (active) { if (Discover) _ = LoadPageAsync(); else if (FavoritesButton.IsChecked == true) ShowFavorites(); }
    }
    public void CancelOperation() { install?.Cancel(); browse?.Cancel(); debounce?.Cancel(); }
    private void ViewLoaded(object sender, RoutedEventArgs e)
    {
        active = true; if (!initialized) return;
        imageRequests?.Dispose(); imageRequests = new();
        if (Discover && renderedScope != store!.Current.Id + Kind) _ = LoadPageAsync(); else LoadVisiblePreviews();
    }
    private void ViewUnloaded(object sender, RoutedEventArgs e)
    {
        active = false; requestId++; browse?.Cancel(); debounce?.Cancel(); imageRequests?.Cancel(); install?.Cancel();
        foreach (var preview in previews) preview.Image.Source = null; requested.Clear();
    }
    private void KindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return; offset = 0; updates.Clear(); renderedScope = null;
        PackFilters.Visibility = ResolutionFilter.Visibility = Kind == "resourcepack" ? Visibility.Visible : Visibility.Collapsed;
        ScopeNote.Text = Kind == "resourcepack" ? "Modrinth · Minecraft 1.8.9 · INSTALL enables at highest priority. UPDATE preserves ON/OFF and priority. Applies on the next launch." : "Modrinth · Forge 1.8.9 / Java 8 · Required pinned dependencies are verified together. Changes apply on the next launch.";
        RefreshInstalled(); if (Discover) _ = LoadPageAsync(); else if (FavoritesButton.IsChecked == true) ShowFavorites();
    }
    private void SectionChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return; browse?.Cancel(); debounce?.Cancel(); requestId++;
        DiscoverTools.Visibility = Discover ? Visibility.Visible : Visibility.Collapsed;
        InstalledTools.Visibility = InstalledButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        InstalledRows.Visibility = InstalledButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Cards.Visibility = InstalledButton.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        Pagination.Visibility = Discover ? Visibility.Visible : Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        if (Discover) _ = LoadPageAsync();
        else if (FavoritesButton.IsChecked == true) ShowFavorites();
        else { RefreshInstalled(); LibraryStatus.Text = "Installed content is available offline. Updates are checked only when requested."; }
    }
    private void FilterChanged(object sender, SelectionChangedEventArgs e) { if (initialized && active && Discover) { offset = 0; _ = LoadPageAsync(); } }
    private async void QueryChanged(object sender, TextChangedEventArgs e)
    {
        if (!initialized || !active || !Discover) return;
        debounce?.Cancel(); var pending = debounce = new CancellationTokenSource();
        try { await Task.Delay(400, pending.Token); offset = 0; await LoadPageAsync(); }
        catch (OperationCanceledException) { }
        finally { if (debounce == pending) debounce = null; pending.Dispose(); }
    }
    private async void QueryKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; debounce?.Cancel(); offset = 0; await LoadPageAsync(); } }
    private async void SearchClick(object sender, RoutedEventArgs e) { debounce?.Cancel(); await LoadPageAsync(); }
    private void ClearClick(object sender, RoutedEventArgs e) { ContentQuery.Text = ""; Sort.SelectedIndex = Category.SelectedIndex = Resolution.SelectedIndex = 0; offset = 0; _ = LoadPageAsync(); }
    private async void PreviousClick(object sender, RoutedEventArgs e) { offset = Math.Max(0, offset - 12); await LoadPageAsync(); ContentQuery.Focus(); }
    private async void NextClick(object sender, RoutedEventArgs e) { offset += 12; await LoadPageAsync(); ContentQuery.Focus(); }
    private async Task LoadPageAsync()
    {
        if (!initialized || !active || !Discover) return;
        browse?.Cancel(); var source = browse = new CancellationTokenSource(); var version = ++requestId; var profile = store!.Current.Id; var kind = Kind;
        var query = new ContentQuery(ContentQuery.Text, kind, ((ComboBoxItem)Sort.SelectedItem).Tag.ToString()!, offset, 12, kind == "resourcepack" ? ((ComboBoxItem)Category.SelectedItem).Tag.ToString() : null, kind == "resourcepack" ? ((ComboBoxItem)Resolution.SelectedItem).Tag.ToString() : null);
        LibraryStatus.Text = "Loading " + (kind == "mod" ? "Forge mods" : "resource packs") + " for Minecraft 1.8.9…";
        RetryButton.Visibility = Visibility.Collapsed; if (!IsMutating) { ContentProgressBar.Visibility = CancelContent.Visibility = Visibility.Visible; ContentProgressBar.IsIndeterminate = true; }
        PreviousPage.IsEnabled = NextPage.IsEnabled = false;
        if (Cards.Children.Count == 0) Skeletons();
        try
        {
            var page = await content!.BrowseAsync(query, source.Token);
            if (version != requestId || profile != store.Current.Id || !Discover || kind != Kind) return;
            total = page.Total; renderedScope = profile + kind; RenderCards(page.Hits);
            PageLabel.Text = total == 0 ? "0 results" : $"Page {offset / 12 + 1} · {total:N0} results";
            PreviousPage.IsEnabled = offset > 0; NextPage.IsEnabled = offset + 12 < total;
            LibraryStatus.Text = page.Hits.Length == 0 ? "No compatible results. Clear filters or try a shorter search." : "Choose by preview. Press INSTALL on a card to add it to " + store.Current.Name + ".";
        }
        catch (OperationCanceledException) { if (version == requestId) LibraryStatus.Text = "Browsing cancelled. Retry to load content."; }
        catch (Exception ex)
        {
            if (version != requestId) return;
            if (renderedScope is null) { Cards.Children.Clear(); Cards.Children.Add(Text("Unable to connect. Installed content and local import are still available.", 18)); }
            LibraryStatus.Text = "Unable to load Discover: " + ex.Message + (renderedScope is null ? "" : " Previously loaded results remain visible; they may be stale.");
            RetryButton.Visibility = Visibility.Visible;
        }
        finally
        {
            if (browse == source) { browse = null; if (!IsMutating) ContentProgressBar.Visibility = CancelContent.Visibility = Visibility.Collapsed; }
            source.Dispose();
        }
    }
    private static TextBlock Text(string value, double size = 14) => new() { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 3, 4, 5) };
    private static Button Button(string text, string accessible, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6), MinHeight = 36 };
        AutomationProperties.SetName(button, accessible); button.Click += click; return button;
    }
    private static Brush BrushResource(object key) => (Brush)Application.Current.FindResource(key);
    private void Skeletons()
    {
        Cards.Children.Clear(); for (var i = 0; i < 3; i++) Cards.Children.Add(new Border { Height = 335, Width = 270, Margin = new Thickness(6), Background = BrushResource(SystemColors.ControlBrushKey), Child = Text("Loading preview…", 18) });
    }
    private void RenderCards(IEnumerable<SearchHit> hits)
    {
        imageRequests?.Cancel(); imageRequests?.Dispose(); imageRequests = new(); previews.Clear(); requested.Clear(); actions.Clear(); Cards.Children.Clear();
        foreach (var hit in hits.Take(200))
        {
            var panel = new StackPanel();
            var imageArea = new Grid { Height = 145, Margin = new Thickness(4, 4, 4, 8), Background = BrushResource(SystemColors.ControlBrushKey) };
            var fallback = Text("Preview unavailable", 14); fallback.VerticalAlignment = VerticalAlignment.Center; fallback.HorizontalAlignment = HorizontalAlignment.Center;
            var image = new Image { Stretch = Stretch.Uniform, Height = 145 }; AutomationProperties.SetName(image, hit.Title + " preview");
            imageArea.Children.Add(fallback); imageArea.Children.Add(image); panel.Children.Add(imageArea); previews.Add((image, hit.ThumbnailUrl ?? hit.IconUrl ?? hit.Gallery.FirstOrDefault(), fallback));
            panel.Children.Add(Button(hit.Title, "Details for " + hit.Title, async (_, _) => await DetailsAsync(hit)));
            if (hit.Author is not null) panel.Children.Add(Text("by " + hit.Author));
            var description = Text(hit.Description); description.MaxHeight = 54; description.TextTrimming = TextTrimming.CharacterEllipsis; description.ToolTip = hit.Description; panel.Children.Add(description);
            panel.Children.Add(Text("1.8.9" + (hit.Kind == "mod" ? " · Forge" : "") + " · " + string.Join(" · ", hit.Categories.Take(3))));
            panel.Children.Add(Text((hit.Downloads.HasValue ? hit.Downloads.Value.ToString("N0") + " downloads · " : "") + (hit.Updated.HasValue ? "Updated " + hit.Updated.Value.ToString("yyyy-MM-dd") : "") + " · Modrinth", 12));
            var state = Text(""); panel.Children.Add(state);
            var row = new WrapPanel();
            var installButton = Button("INSTALL", "Install " + hit.Title, async (_, _) => await CardInstallAsync(hit)); installButton.SetResourceReference(Control.BackgroundProperty, "Accent"); row.Children.Add(installButton);
            actions[hit.ProjectId] = (installButton, state, hit.Title);
            var favorite = new CheckBox { Content = "Favorite", IsChecked = favorites.Any(f => f.ProjectId == hit.ProjectId && f.Kind == hit.Kind), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
            AutomationProperties.SetName(favorite, "Favorite " + hit.Title);
            RoutedEventHandler favoriteChanged = (_, _) => SaveFavorite(hit, favorite.IsChecked == true); favorite.Checked += favoriteChanged; favorite.Unchecked += favoriteChanged; row.Children.Add(favorite); panel.Children.Add(row);
            Cards.Children.Add(new Border { Child = panel, Margin = new Thickness(6), Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = BrushResource(SystemColors.ControlDarkBrushKey), Background = BrushResource(SystemColors.WindowBrushKey) });
        }
        UpdateCardStates(); ResizeCards(); LibraryScroll.ScrollToTop(); Dispatcher.BeginInvoke(LoadVisiblePreviews);
    }
    private void UpdateCardStates()
    {
        if (!initialized) return;
        foreach (var pair in actions)
        {
            var record = store!.Current.Content.FirstOrDefault(c => c.ProjectId == pair.Key && c.Kind == Kind);
            pair.Value.Button.Content = record is null ? "INSTALL" : updates.ContainsKey(pair.Key) ? "UPDATE" : "INSTALLED";
            pair.Value.Button.IsEnabled = !IsMutating && (record is null || updates.ContainsKey(pair.Key));
            pair.Value.State.Text = record is null ? "" : updates.ContainsKey(pair.Key) ? "Update available · " + record.VersionNumber : "Installed · " + record.VersionNumber;
            AutomationProperties.SetName(pair.Value.Button, (record is null ? "Install " : updates.ContainsKey(pair.Key) ? "Update " : "Installed ") + (record?.Title ?? pair.Value.Title));
        }
    }
    private async Task CardInstallAsync(SearchHit hit)
    {
        var record = store!.Current.Content.FirstOrDefault(c => c.ProjectId == hit.ProjectId && c.Kind == hit.Kind);
        await MutateAsync(ct => record is null ? content!.InstallAsync(hit.ProjectId, hit.Kind, ct, progress: Feedback()) : content!.UpdateAsync(record, ct, Feedback()), record is null ? "Installed " + hit.Title + (hit.Kind == "resourcepack" ? " · enabled at highest priority." : ".") : "Updated " + hit.Title + " · enabled state and priority preserved.");
    }
    private IProgress<ContentProgress> Feedback() => new Progress<ContentProgress>(p =>
    {
        LibraryStatus.Text = p.Stage + " · " + p.Title;
        ContentProgressBar.IsIndeterminate = !p.Total.HasValue || p.Total <= 0;
        if (!ContentProgressBar.IsIndeterminate) { ContentProgressBar.Maximum = p.Total!.Value; ContentProgressBar.Value = p.Received ?? 0; }
    });
    private async Task MutateAsync(Func<CancellationToken, Task> action, string success)
    {
        if (IsMutating || !initialized) return;
        install = new(); BusyChanged?.Invoke(true); ContentProgressBar.Visibility = CancelContent.Visibility = Visibility.Visible;
        ContentProgressBar.IsIndeterminate = true; ImportButton.IsEnabled = CheckUpdatesButton.IsEnabled = ContentKind.IsEnabled = false;
        UpdateCardStates(); RefreshInstalled();
        try { await action(install.Token); updates.Clear(); LibraryStatus.Text = success + " Changes apply on the next launch."; Changed?.Invoke(LibraryStatus.Text); }
        catch (OperationCanceledException) { LibraryStatus.Text = "Cancelled. Existing content has been preserved."; Changed?.Invoke(LibraryStatus.Text); }
        catch (Exception ex) { LibraryStatus.Text = "Unable to install or update: " + ex.Message + " Existing content has been preserved; retry when corrected."; Changed?.Invoke(LibraryStatus.Text); }
        finally
        {
            install.Dispose(); install = null; BusyChanged?.Invoke(false); ContentProgressBar.Visibility = CancelContent.Visibility = Visibility.Collapsed;
            ImportButton.IsEnabled = CheckUpdatesButton.IsEnabled = ContentKind.IsEnabled = true; UpdateCardStates(); RefreshInstalled();
        }
    }
    private void RefreshInstalled()
    {
        if (!initialized) return; InstalledRows.Children.Clear();
        var entries = content!.Installed(Kind); if (entries.Length == 0) InstalledRows.Children.Add(Text("No content installed in " + store!.Current.Name + ". Browse Discover or import a local file.", 18));
        foreach (var entry in entries)
        {
            var panel = new StackPanel { Margin = new Thickness(8) }; var title = entry.Managed?.Title ?? entry.File;
            panel.Children.Add(Text(title, 18));
            panel.Children.Add(Text((entry.Enabled ? "ON" : "OFF") + (entry.Priority.HasValue ? " · Priority " + entry.Priority : "") + " · " + (entry.Managed is null ? "Local · no provider updates" : "Managed · Modrinth · " + entry.Managed.VersionNumber)));
            if (entry.Managed is not null && updates.ContainsKey(entry.Managed.ProjectId)) panel.Children.Add(Text("Update available · " + updates[entry.Managed.ProjectId].Number));
            panel.Children.Add(Text(entry.File, 12));
            var row = new WrapPanel();
            var enabled = new CheckBox { Content = "Enabled", IsChecked = entry.Enabled, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center, IsEnabled = !IsMutating };
            AutomationProperties.SetName(enabled, "Enable " + title);
            RoutedEventHandler enabledChanged = (_, _) => Local(() => content.Toggle(entry.File, entry.Kind), "Content state saved."); enabled.Checked += enabledChanged; enabled.Unchecked += enabledChanged; row.Children.Add(enabled);
            void Add(string label, string name, RoutedEventHandler click, bool available = true) { var b = Button(label, name + " " + title, click); b.IsEnabled = available && !IsMutating; row.Children.Add(b); }
            if (entry.Kind == "resourcepack")
            {
                Add("Move up", "Increase priority of", (_, _) => Local(() => content.MovePack(entry.File, -1), "Pack priority saved."), entry.Priority > 1);
                Add("Move down", "Decrease priority of", (_, _) => Local(() => content.MovePack(entry.File, 1), "Pack priority saved."), entry.Priority.HasValue && entry.Priority < store!.Current.Packs.Count);
                Add("Preview", "Preview", (_, _) => Preview(entry));
            }
            if (entry.Managed is not null)
            {
                Add("Details", "Details for", async (_, _) => await DetailsAsync(new(entry.Managed.ProjectId, title, "", entry.Kind)));
                if (updates.ContainsKey(entry.Managed.ProjectId)) Add("UPDATE", "Update", async (_, _) => await MutateAsync(ct => content.UpdateAsync(entry.Managed, ct, Feedback()), "Updated " + title + "; ON/OFF and priority preserved."));
            }
            Add("Remove", "Remove", (_, _) =>
            {
                var owner = Window.GetWindow(this);
                if (MessageBox.Show(owner, "Remove " + title + " from " + store!.Current.Name + "? A backup is retained.", "Remove content", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
                    Local(() => content.Delete(entry.File, entry.Kind), "Removed from this profile; backup retained.");
            });
            panel.Children.Add(row); InstalledRows.Children.Add(new Border { Child = panel, Margin = new Thickness(4, 4, 4, 8), BorderThickness = new Thickness(1), BorderBrush = BrushResource(SystemColors.ControlDarkBrushKey), Background = BrushResource(SystemColors.WindowBrushKey) });
        }
    }
    private void Local(Action action, string message)
    {
        if (IsMutating) return;
        try { action(); LibraryStatus.Text = message + " Applies on the next launch."; Changed?.Invoke(LibraryStatus.Text); UpdateCardStates(); RefreshInstalled(); InstalledButton.Focus(); }
        catch (Exception ex) { LibraryStatus.Text = ex.Message; Changed?.Invoke(ex.Message); RefreshInstalled(); }
    }
    private async void ImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = Kind == "mod" ? "Forge 1.8.9 mod|*.jar" : "Minecraft 1.8.9 resource pack|*.zip" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) { var kind = Kind; await MutateAsync(ct => Task.Run(() => { ct.ThrowIfCancellationRequested(); content!.Import(dialog.FileName, kind); }, ct), "Local content imported and validated."); }
    }
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshInstalled();
    private async void CheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (IsMutating) return; browse?.Cancel(); var source = browse = new(); var profile = store!.Current.Id; var kind = Kind;
        CheckUpdatesButton.IsEnabled = false; LibraryStatus.Text = "Checking compatible stable updates…"; CancelContent.Visibility = Visibility.Visible;
        try { var result = await content!.CheckUpdatesAsync(kind, source.Token); if (profile != store.Current.Id || kind != Kind) return; updates.Clear(); foreach (var pair in result) updates[pair.Key] = pair.Value; LibraryStatus.Text = result.Count == 0 ? "No newer compatible stable updates found." : result.Count + " update(s) available. UPDATE preserves enabled state and priority."; UpdateCardStates(); RefreshInstalled(); }
        catch (OperationCanceledException) { LibraryStatus.Text = "Update check cancelled."; }
        catch (Exception ex) { LibraryStatus.Text = "Unable to check updates: " + ex.Message + " Installed content is available offline."; }
        finally { if (browse == source) browse = null; source.Dispose(); CheckUpdatesButton.IsEnabled = true; if (!IsMutating) CancelContent.Visibility = Visibility.Collapsed; }
    }
    private void CancelClick(object sender, RoutedEventArgs e) => CancelOperation();
    private void Preview(InstalledContent entry)
    {
        try
        {
            using var zip = ZipFile.OpenRead(Path.Combine(content!.DirectoryFor(entry.Kind), entry.File)); var icon = zip.GetEntry("pack.png");
            var panel = new StackPanel { Margin = new Thickness(16) }; panel.Children.Add(Text(entry.Managed?.Title ?? entry.File, 23));
            if (icon is not null && icon.Length <= 2 * 1024 * 1024) { using var stream = icon.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes); panel.Children.Add(new Image { Source = ContentImages.Decode(bytes.ToArray()), Height = 240, Stretch = Stretch.Uniform }); }
            else panel.Children.Add(Text("This pack has no supported pack.png preview."));
            ShowDialog("Pack preview", panel);
        }
        catch (Exception ex) { LibraryStatus.Text = "Preview unavailable: " + ex.Message; }
    }
    private async Task DetailsAsync(SearchHit hit)
    {
        var owner = Window.GetWindow(this); var panel = new StackPanel { Margin = new Thickness(16) }; panel.Children.Add(Text(hit.Title, 25));
        var summary = Text("Loading details…"); panel.Children.Add(summary);
        var dialog = CreateDialog("Content details", panel); using var source = new CancellationTokenSource(); dialog.Closed += (_, _) => source.Cancel();
        async Task Load()
        {
            try
            {
                var details = await content!.DetailsAsync(hit.ProjectId, hit.Kind, source.Token); source.Token.ThrowIfCancellationRequested();
                summary.Text = (hit.Author is null ? "" : "by " + hit.Author + " · ") + "Modrinth · " + (details.Project.Downloads?.ToString("N0") ?? "Unknown") + " downloads\nLicense: " + (details.Project.License ?? "Not supplied") + "\nSupported versions: " + string.Join(", ", details.Versions);
                var record = store!.Current.Content.FirstOrDefault(c => c.ProjectId == hit.ProjectId && c.Kind == hit.Kind);
                panel.Children.Add(Text(record is null ? "Not installed in this profile." : "Installed version: " + record.VersionNumber));
                var versions = await content.VersionsForDetailsAsync(hit.ProjectId, hit.Kind, source.Token);
                panel.Children.Add(Text("Latest compatible stable release: " + (versions ?? "Unavailable")));
                var installButton = Button(record is null ? "INSTALL" : "Check / UPDATE", (record is null ? "Install " : "Update ") + hit.Title, async (_, _) => { dialog.Close(); await CardInstallAsync(hit); });
                installButton.IsEnabled = !IsMutating; panel.Children.Add(installButton);
                panel.Children.Add(Button("Open provider page", "Open Modrinth page for " + hit.Title, (_, _) => Process.Start(new ProcessStartInfo("https://modrinth.com/" + hit.Kind + "/" + Uri.EscapeDataString(hit.ProjectId)) { UseShellExecute = true })));
                // Provider body is text, never active HTML/scripts in the launcher.
                panel.Children.Add(Text(details.Body.Length > 24000 ? details.Body[..24000] + "\nRead the full description on Modrinth." : details.Body));
                foreach (var url in details.Project.Gallery.Take(4))
                {
                    var image = new Image { Height = 230, Stretch = Stretch.Uniform, Margin = new Thickness(4, 12, 4, 4) }; AutomationProperties.SetName(image, hit.Title + " gallery image"); panel.Children.Add(image);
                    image.Loaded += async (_, _) => { try { if (source.IsCancellationRequested) return; image.Source = await images!.LoadAsync(url, source.Token); } catch (OperationCanceledException) { } };
                }
                if (details.Project.Gallery.Length == 0 && details.Project.IconUrl is not null) { var icon = new Image { Height = 180, Stretch = Stretch.Uniform, Source = await images!.LoadAsync(details.Project.IconUrl, source.Token) }; panel.Children.Add(icon); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!source.IsCancellationRequested) summary.Text = "Unable to load details: " + ex.Message + ". Close and retry; installed content is available offline."; }
        }
        var loading = Load(); dialog.ShowDialog(); await loading; ContentQuery.Focus();
    }
    private Window CreateDialog(string title, StackPanel panel)
    {
        var dialog = new Window { Title = title, Owner = Window.GetWindow(this), Width = 620, Height = 620, MinWidth = 360, MinHeight = 300, Background = BrushResource(SystemColors.WindowBrushKey), Foreground = BrushResource(SystemColors.WindowTextBrushKey) };
        panel.Children.Add(Button("Close", "Close " + title, (_, _) => dialog.Close())); dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } }; return dialog;
    }
    private void ShowDialog(string title, StackPanel panel) { var dialog = CreateDialog(title, panel); dialog.ShowDialog(); InstalledButton.Focus(); }
    private void SaveFavorite(SearchHit hit, bool enabled)
    {
        try
        {
            var existing = favorites.FirstOrDefault(f => f.ProjectId == hit.ProjectId && f.Kind == hit.Kind); if (existing is not null) favorites.Remove(existing);
            if (enabled) { if (favorites.Count >= 200) throw new InvalidOperationException("Favorite limit reached (200). Remove a favorite first."); favorites.Add(hit); }
            ProfileStore.AtomicWrite(FavoritesPath, JsonSerializer.Serialize(favorites)); LibraryStatus.Text = enabled ? "Saved to local Favorites." : "Favorite removed.";
        }
        catch (Exception ex) { LibraryStatus.Text = "Unable to save favorite: " + ex.Message; }
    }
    private void ShowFavorites() { RenderCards(favorites.Where(f => f.Kind == Kind)); LibraryStatus.Text = favorites.Any(f => f.Kind == Kind) ? "Local Favorites · stored provider metadata may be stale. INSTALL always checks current compatible releases." : "No favorites yet. Use Favorite on a Discover card."; }
    private void CardsSizeChanged(object sender, SizeChangedEventArgs e) => ResizeCards();
    private void ResizeCards()
    {
        var available = Math.Max(270, LibraryScroll.ActualWidth - 22); var columns = Math.Max(1, (int)(available / 285));
        foreach (FrameworkElement card in Cards.Children) card.Width = Math.Max(250, available / columns - 12);
    }
    private void ScrollChanged(object sender, ScrollChangedEventArgs e) => LoadVisiblePreviews();
    private async void LoadVisiblePreviews()
    {
        if (!active || imageRequests is null) return; var token = imageRequests.Token;
        foreach (var preview in previews.ToArray())
        {
            if (requested.Contains(preview.Image) || !preview.Image.IsVisible || !preview.Image.IsLoaded) continue;
            var bounds = preview.Image.TransformToAncestor(LibraryScroll).TransformBounds(new Rect(0, 0, preview.Image.ActualWidth, preview.Image.ActualHeight));
            if (bounds.Bottom < 0 || bounds.Top > LibraryScroll.ViewportHeight) continue;
            requested.Add(preview.Image);
            try { var image = await images!.LoadAsync(preview.Url, token); if (!token.IsCancellationRequested) { preview.Image.Source = image; preview.Fallback.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed; } }
            catch (OperationCanceledException) { return; }
            catch (Exception) { /* A missing preview never disables Install. */ }
        }
    }
}

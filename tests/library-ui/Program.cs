using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Ttro.Launcher;
using Ttro.Launcher.Core;

// Test-only WPF host. The shipping launcher never loads fixture providers or bypasses login.
public static class LibraryQaHost
{
    [STAThread]
    public static void Main()
    {
        var app = new App(); app.InitializeComponent(); app.StartupUri = null;
        var root = Environment.GetEnvironmentVariable("TTRO_LIBRARY_QA_ROOT") ?? throw new InvalidOperationException("A scratch QA root is required.");
        var store = new ProfileStore(root, Path.Combine(AppContext.BaseDirectory, "modules.json"));
        var fixture = new ContentFixture(); var service = new ContentService(store, fixture, fixture.Http);
        var library = new LibraryView(); library.Initialize(store, service, new ContentImages(root, fixture.Http));
        var dock = new DockPanel(); var tools = new WrapPanel(); DockPanel.SetDock(tools, Dock.Top); dock.Children.Add(tools);
        Button Add(string text, Action action) { var b = new Button { Content = text, Margin = new Thickness(4) }; b.Click += (_, _) => action(); tools.Children.Add(b); return b; }
        Add("Use v2", () => fixture.Revision = 2);
        Add("Offline", () => fixture.Offline = true); Add("Online", () => fixture.Offline = false);
        Add("Slow download", () => fixture.Slow = true); Add("Fast download", () => fixture.Slow = false);
        Add("Other profile", () => { store.Add("QA other"); library.RefreshProfile(); });
        var imageState = new TextBlock { Text = "Loaded previews: 0", Margin = new Thickness(6) }; AutomationProperties.SetAutomationId(imageState, "ImageState"); tools.Children.Add(imageState);
        dock.Children.Add(library);
        var window = new Window { Title = "Ttro Library QA", Width = 1100, Height = 760, MinWidth = 620, MinHeight = 500, Content = dock, Background = (Brush)app.Resources["Surface"], Foreground = (Brush)app.Resources["Ink"], FontSize = 15 };
        static IEnumerable<DependencyObject> Children(DependencyObject parent) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Children(child)) yield return nested; } }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += (_, _) => imageState.Text = "Loaded previews: " + Children(library).OfType<Image>().Count(i => i.Source is not null); timer.Start();
        window.Closing += (_, e) => { if (library.IsMutating) { e.Cancel = true; library.CancelOperation(); } };
        app.Run(window); timer.Stop();
    }
}

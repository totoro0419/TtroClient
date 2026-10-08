using System.Windows;
using System.Windows.Media;
namespace Ttro.Launcher;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  if (SystemParameters.HighContrast) { Resources["Surface"] = SystemColors.WindowBrush; Resources["Ink"] = SystemColors.WindowTextBrush; Resources["Accent"] = SystemColors.HighlightBrush; }
  base.OnStartup(e);
 }
}

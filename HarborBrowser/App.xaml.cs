using System.Runtime.InteropServices;
using System.Windows;
namespace HarborBrowser
{
    public partial class App:Application
    {
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        protected override void OnStartup(StartupEventArgs e)
        {
            SetCurrentProcessExplicitAppUserModelID("HarborBrowser.Desktop");
            base.OnStartup(e);
        }
    }
}

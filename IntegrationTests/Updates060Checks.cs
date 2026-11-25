using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Updates;
using HarborBrowser.Views;
internal static partial class Program
{
    private sealed class ForbiddenUpdateTransport:IUpdateTransport
    {
        internal int Calls;
        public Task<UpdateResponse> Open(Uri u,long offset,string etag,CancellationToken c){Calls++;throw new Exception("A paused update must never send a request.");}
    }
    private static async Task Updates060Checks(MainWindow main,BrowserDataStore store)
    {
        var menu=(ContextMenu)typeof(MainWindow).GetMethod("BuildGearMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
        var help=menu.Items.OfType<MenuItem>().First(m=>Label(m)=="Help");var tools=menu.Items.OfType<MenuItem>().First(m=>Label(m)=="Tools");
        Check("Help exposes Check for updates",help.Items.OfType<MenuItem>().Any(m=>Label(m)=="Check for updates…"));
        Check("Tools exposes updates and backup recovery",tools.Items.OfType<MenuItem>().Any(m=>Label(m)=="Updates and backup recovery…"));
        Check("regular browsing allows explicit update traffic",main.UpdateNetworkAllowed);
        var field=typeof(MainWindow).GetField("_updateTraffic",BindingFlags.Instance|BindingFlags.NonPublic);
        using(var cancel=new CancellationTokenSource())
        {
            field.SetValue(main,cancel);
            try
            {
                await main.ChangeProtonPreview(true);
                Check("enabling native Proton cancels pending update traffic",cancel.IsCancellationRequested);
                Check("enabled unverified Proton blocks update traffic",!main.UpdateNetworkAllowed);
                var transport=new ForbiddenUpdateTransport();var service=new UpdateService(store.RootFolder,()=>main.UpdateNetworkAllowed,transport);
                bool refused=false;try{await service.Check(CancellationToken.None);}catch(InvalidOperationException){refused=true;}
                Check("paused updater sends no native network request",refused&&transport.Calls==0);
                var window=new UpdatesWindow(service,AppVersion.Number,CancellationToken.None);Check("native protection disables update check button",!window.CheckButton.IsEnabled);window.Close();
                await main.ChangeProtonPreview(false);Check("explicit Proton removal restores update availability",main.UpdateNetworkAllowed);
            }
            finally{field.SetValue(main,null);}
        }
    }
}

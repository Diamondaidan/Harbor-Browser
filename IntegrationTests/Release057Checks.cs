using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;

internal static partial class Program
{
    private static async Task Release057Checks(MainWindow main,BrowserDataStore store,string output)
    {
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;
        Check("057 native recovery profile is owned by this test",Path.GetFullPath(core.Environment.UserDataFolder).StartsWith(Path.GetFullPath(store.RootFolder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
        string root=Path.GetPathRoot(Path.GetFullPath(output));
        Check("057 stable profile preserves the established Harbor location",ProtonTrialIdentity.DataFolder(root)==Path.Combine(root,"HarborBrowser"));
        Check("057 stable application and IPC identities match installed Harbor",ProtonTrialIdentity.AppUserModelId=="HarborBrowser.Desktop"&&ProtonTrialIdentity.InstanceSuffix==""&&ProtonTrialIdentity.RegisteredName=="Harbor Browser");
        using(var server=new BetaGuardFixtureServer())
        {
            main.AutomaticProtonMonitoringEnabled=false;
            var validator=main.ProtonProxyOwnershipValidator;
            main.ProtonProxyOwnershipValidator=cancel=>Task.FromResult(true);
            long epoch=main.BeginProtonProtection(true);main.MarkProtonRouteVerified(epoch);
            try
            {
                string target=server.Url("recovery057/document");
                await AuthNavigate(core,target);
                int before=server.Count("recovery057/document");
                Check("057 crash fixture has a functional request positive control",before==1);
                try{await ProtonRouteProbe.Bounded(core.CallDevToolsProtocolMethodAsync("Browser.crash","{}"),5000,CancellationToken.None);}catch{}
                await Wait(()=>tab.NeedsNewView&&tab.FailureMessage!=null);
                Check("actual owned engine crash closes the protection gate",main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
                Check("engine crash retains the owned tab destination",tab.PendingUrl==target);
                await main.RecoverTab(tab);
                await Wait(()=>tab.Initialized);
                await Task.Delay(200);
                Check("recovering an unverified tab stays paused",tab.ProtonPaused&&main.ProtonProtection.State!=ProtonProtectionPhase.Protected);
                Check("engine recovery does not replay the old request",server.Count("recovery057/document")==before);
                await main.ChangeProtonPreview(false);
                typeof(MainWindow).GetMethod("Reload_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,new object[]{null,null});
                await Wait(()=>server.Count("recovery057/document")==before+1&&!tab.ProtonPaused);
                Check("explicit off and Reload recover the saved destination",tab.View.CoreWebView2.Source==target);
                File.WriteAllText(Path.Combine(output,"recovery057.json"),Json.Serialize(new{ActualOwnedEngineCrash=true,TabPreserved=true,PausedAfterRecovery=true,NoAutomaticReplay=true,ExplicitRetryWorked=true,SyntheticProof=true,AccountLogin=false,UserProfileAccess=false}));
            }
            finally{main.ProtonProxyOwnershipValidator=validator;main.BeginProtonProtection(false);}
        }
    }
}

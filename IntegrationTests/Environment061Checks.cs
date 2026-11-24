using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarborBrowser;
using HarborBrowser.Models;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Win32;

internal static partial class Program
{
    private static async Task Environment061Checks(MainWindow main,string output)
    {
        ProtectionClock061Checks();
        bool monitoring=main.AutomaticProtonMonitoringEnabled;
        var validator=main.ProtonProxyOwnershipValidator;
        var lease=main.ProtonVerificationLease;
        var tab=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var core=tab.View.CoreWebView2;
        main.AutomaticProtonMonitoringEnabled=false;
        main.ProtonVerificationLease=TimeSpan.FromMinutes(2);
        main.ProtonProxyOwnershipValidator=cancel=>Task.FromResult(true);
        try
        {
            using(var server=new BetaGuardFixtureServer())
            {
                string[] events={"network-lost","network-returned","address-change","suspend","resume"};
                foreach(string change in events)
                {
                    long epoch=main.ProtonProtection.Enabled?main.ProtonProtection.Epoch:main.BeginProtonProtection(true);
                    Check(change+" positive route proof is accepted",main.MarkProtonRouteVerified(epoch));
                    string prefix="environment-"+change;
                    await BetaNavigate(core,server.Url(prefix+"/page"));
                    await WaitJs(core,"betaFixture.ready");
                    Check(change+" positive navigation reaches the local fixture",server.Count(prefix+"/page")==1);
                    typeof(MainWindow).GetField("_protonNextCheckUtc",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,DateTime.UtcNow.AddMinutes(1));
                    using(var pending=new CancellationTokenSource())
                    {
                        typeof(MainWindow).GetField("_protonProbeCancellation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,pending);
                        // Deliberately hold the UI dispatcher while Windows-like
                        // callbacks arrive on another thread. The request gate
                        // must close before queued visual work can run.
                        var callback=Task.Run(()=>{
                            if(change=="suspend"||change=="resume")
                                typeof(MainWindow).GetMethod("TorPowerModeChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{null,new PowerModeChangedEventArgs(change=="suspend"?PowerModes.Suspend:PowerModes.Resume)});
                            else if(change=="address-change")
                                typeof(MainWindow).GetMethod("TorNetworkAddressChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{null,EventArgs.Empty});
                            else
                                typeof(MainWindow).GetMethod("TorNetworkAvailabilityChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{null,Activator.CreateInstance(typeof(NetworkAvailabilityEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{change!="network-lost"},null)});
                            return main.ProtonProtection.ShouldBlock(server.Url(prefix+"/blocked"),false);
                        });
                        if(!callback.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("The environment callback blocked on the interface thread.");
                        Check(change+" closes the request gate before the interface can update",callback.GetAwaiter().GetResult());
                        Check(change+" rejects the pre-change route result",!main.MarkProtonRouteVerified(epoch));
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Check(change+" cancels obsolete route verification",pending.IsCancellationRequested);
                        Check(change+" clears the old verification cooldown",(DateTime)Field(main,"_protonNextCheckUtc")==DateTime.MinValue);
                        typeof(MainWindow).GetField("_protonProbeCancellation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,null);
                    }
                    await Wait(()=>tab.ProtonPaused&&core.Source=="about:blank");
                    Check(change+" blocked request cannot be approved",!await main.CanSendProtonRequestAsync(server.Url(prefix+"/blocked"),false,CancellationToken.None));
                    core.Navigate(server.Url(prefix+"/blocked"));
                    await Task.Delay(200);
                    Check(change+" blocked destination never reaches the local server",server.Count(prefix+"/blocked")==0);
                    if(change=="network-lost"||change=="suspend")
                    {
                        Check(change+" cannot accept a proof while the adapter is unavailable",!main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                        typeof(MainWindow).GetMethod("QueueTorEnvironmentChange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{true,"Owned network returned before fresh proof"});
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    }
                    Check(change+" fresh route evidence can restore protection",main.MarkProtonRouteVerified(main.ProtonProtection.Epoch));
                    await Task.Delay(100);
                    Check(change+" recovery does not replay the paused destination",tab.ProtonPaused&&server.Count(prefix+"/page")==1&&server.Count(prefix+"/blocked")==0);
                    typeof(MainWindow).GetMethod("Reload_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{null,new RoutedEventArgs()});
                    await Wait(()=>server.Count(prefix+"/page")==2);
                    await WaitJs(core,"document.readyState==='complete'&&betaFixture.ready");
                    Check(change+" explicit reload reopens the original address exactly once",server.Count(prefix+"/page")==2&&server.Count(prefix+"/blocked")==0);
                }
                main.BeginProtonProtection(false);
                typeof(MainWindow).GetMethod("QueueTorEnvironmentChange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{false,"Owned off-state event"});
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check("environment events do not enable protection or block regular browsing when it is off",main.ProtonProtection.State==ProtonProtectionPhase.Off&&!main.ProtonProtection.ShouldBlock(server.Url("off"),false));
                File.WriteAllText(Path.Combine(output,"environment-scope.txt"),"Synthetic network availability/address and suspend/resume callbacks. Local fixture endpoints only. No account login, physical disconnect, sleep or reboot was performed.");
            }
        }
        finally
        {
            typeof(MainWindow).GetField("_protonProbeCancellation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,null);
            main.BeginProtonProtection(false);main.AutomaticProtonMonitoringEnabled=monitoring;
            main.ProtonProxyOwnershipValidator=validator;main.ProtonVerificationLease=lease;
        }
    }
}

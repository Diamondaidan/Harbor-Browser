using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    private static async Task ProtonGuardHandoffChecks(MainWindow main,string output,bool guarded=true)
    {
        main.AutomaticProtonMonitoringEnabled=false;await main.ChangeProtonPreview(true);
        var home=(BrowserTab)((TabItem)main.BrowserTabs.Items[0]).Tag;
        var environment=(CoreWebView2Environment)Field(main,"_environment");
        await ((ProtonPreviewService)Field(main,"_protonPreview")).Remove(home.View.CoreWebView2.Profile);
        string fixture=Path.Combine(output,"OwnedNativeTabFixture");Directory.CreateDirectory(fixture);
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","BrowserVpn","Proton","manifest.json")));
        File.WriteAllText(Path.Combine(fixture,"manifest.json"),Json.Serialize(new Dictionary<string,object>{{"manifest_version",3},{"name","Harbor owned native tab fixture"},{"version","1.3.6"},{"key",manifest["key"]},{"permissions",new[]{"tabs","storage"}}}));
        File.WriteAllText(Path.Combine(fixture,"popup.html"),"<!doctype html><title>Owned native parent</title><div id='spinner' style='display:none'></div><div id='sign-in-view'><button class='sign-in-button'>Owned test button</button></div>");
        File.WriteAllText(Path.Combine(fixture,"child.html"),"<!doctype html><title>Owned native child</title><body id='owned-child'>Owned child</body><script src='child.js'></script>");
        File.WriteAllText(Path.Combine(fixture,"child.js"),"chrome.storage.local.get('harborOwnedHandoffCanary',v=>{document.body.dataset.canary=v.harborOwnedHandoffCanary||'';});");
        string state=Path.Combine(output,"OwnedService");Directory.CreateDirectory(state);
        using(var service=new ProtonPreviewService(state,Path.GetFullPath(fixture)))
        {
            await service.Enable(home.View.CoreWebView2.Profile);
            var popup=Offscreen(new ProtonPreviewWindow(service,b=>Task.CompletedTask,()=>Task.FromResult(environment),u=>{},null,(source,args,cancel)=>main.HandleNewWindowRequested(source,args,null,cancel,true)));
            popup.ProviderPreparer=async profile=>{await service.Prepare(profile);await main.WarmProtonPopupAsync(profile.ProfileName);};
            popup.Show();await Wait(()=>popup.ControlsReady||popup.ControlsError!=null);
            Check("owned native provider control fixture becomes ready",popup.ControlsReady);
            var created=new TaskCompletionSource<BrowserTab>();var failed=new TaskCompletionSource<string>();BrowserTab child=null;
            main.NewWindowFailureForTesting=(stage,type,code)=>failed.TrySetResult(stage+":"+type+":"+code.ToString("X8"));
            main.NewWindowBoundForTesting=(tab,core)=>created.TrySetResult(tab);
            try
            {
                string canary="owned-"+Guid.NewGuid().ToString("N");var source=popup.ProviderView.CoreWebView2;
                await source.ExecuteScriptAsync("chrome.storage.local.set({harborOwnedHandoffCanary:"+Json.Serialize(canary)+"},()=>window.canaryReady=true)");await WaitJs(source,"window.canaryReady===true");
                await Gesture(source,"chrome.tabs.create({url:'chrome-extension://"+ProtonPreviewService.ExtensionId+"/child.html'},tab=>{const error=chrome.runtime.lastError?.message||'';chrome.storage.local.set({harborOwnedTabCallback:true,harborOwnedTabHasId:Number.isInteger(tab?.id),harborOwnedTabIdTruthy:!!tab?.id,harborOwnedTabError:error,harborOwnedTabType:typeof tab});window.nativeTabReturned=true;});window.close()");
                var first=await Task.WhenAny(created.Task,failed.Task,Task.Delay(40000));
                if(first==failed.Task)throw new Exception("Owned tabs.create failed at "+await failed.Task);
                if(first!=created.Task)throw new TimeoutException("Owned tabs.create did not bind a host view.");
                child=await created.Task;
                await WaitJs(child.View.CoreWebView2,"document.getElementById('owned-child')!==null");
                await WaitJs(child.View.CoreWebView2,"document.body.dataset.canary==="+Json.Serialize(canary));
                await child.View.CoreWebView2.ExecuteScriptAsync("chrome.storage.local.get(['harborOwnedTabCallback','harborOwnedTabHasId','harborOwnedTabIdTruthy','harborOwnedTabError','harborOwnedTabType'],v=>window.ownedCallbackSummary=v)");
                await WaitJs(child.View.CoreWebView2,"window.ownedCallbackSummary!==undefined");
                Console.WriteLine("OWNED CALLBACK "+await child.View.CoreWebView2.ExecuteScriptAsync("window.ownedCallbackSummary"));
                Check("native tabs.create completion is delivered before its source goes away",await child.View.CoreWebView2.ExecuteScriptAsync("window.ownedCallbackSummary.harborOwnedTabCallback===true")=="true");
                Check("native extension tabs.create loads its actual bundled child",true);
                Check("native handoff preserves only the owned extension-storage canary",true);
                Check("native tabs.create callback completes without a runtime error",await child.View.CoreWebView2.ExecuteScriptAsync("window.ownedCallbackSummary.harborOwnedTabError===''")=="true");
                Check("the prepared child survives its provider popup closing",!popup.IsLoaded&&child.Initialized&&!child.IsClosing);
                Check("native extension handoff retains the authentication capability",child.AllowProtonAuthentication);
                Check("armed authentication capability permits only the exact account origin",await main.CanSendProtonRequestAsync("https://account.proton.me/authorize",child.AllowProtonAuthentication,CancellationToken.None)&&!await main.CanSendProtonRequestAsync("https://ordinary.invalid/",false,CancellationToken.None));
                // Exact account names resolve to owned files only in this
                // synthetic profile. No Proton page, challenge or account is
                // contacted or inspected.
                using(var server=new BetaGuardFixtureServer())
                {
                    string blocked=server.Url("auth-unrelated/frame");
                    File.WriteAllText(Path.Combine(fixture,"owned-parent.html"),"<!doctype html><title>Owned authentication parent</title><script>window.parentAlive=true;addEventListener('message',e=>{if(e.origin==='https://account-api.proton.me'&&e.data==='owned-verification-ready')window.ownedVerificationReady=true;});</script><iframe src='https://account-api.proton.me/owned-verification.html'></iframe><iframe src='"+blocked+"'></iframe>");
                    File.WriteAllText(Path.Combine(fixture,"owned-verification.html"),"<!doctype html><title>Owned verification frame</title><script>parent.postMessage('owned-verification-ready','https://account.proton.me');</script>");
                    var core=child.View.CoreWebView2;
                    core.SetVirtualHostNameToFolderMapping("account.proton.me",fixture,CoreWebView2HostResourceAccessKind.Allow);
                    core.SetVirtualHostNameToFolderMapping("account-api.proton.me",fixture,CoreWebView2HostResourceAccessKind.Allow);
                    var denied=new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs> received=(s,e)=>{if(e.Request.Uri==blocked)denied.TrySetResult(e.Response.StatusCode==503);};
                    core.WebResourceResponseReceived+=received;
                    try
                    {
                        await AuthNavigate(core,"https://account.proton.me/owned-parent.html");
                        await WaitJs(core,"window.ownedVerificationReady===true");
                        Check("trusted verification frame loads while ordinary browsing is paused",true);
                        Check("unrelated authentication-page frame receives a denied response",await Task.WhenAny(denied.Task,Task.Delay(15000))==denied.Task&&await denied.Task);
                        Check("denied child frame does not pause or replace its authentication parent",!child.ProtonPaused&&core.Source=="https://account.proton.me/owned-parent.html"&&await core.ExecuteScriptAsync("window.parentAlive===true")=="true");
                        Check("denied authentication-page frame never reaches the owned server",server.Count("auth-unrelated/frame")==0);
                    }
                    finally{core.WebResourceResponseReceived-=received;}
                }
            }
            catch(Exception error){Console.WriteLine("OWNED HANDOFF PRIMARY: "+error.GetType().Name+": "+error.Message);throw;}
            finally
            {
                main.NewWindowBoundForTesting=null;main.NewWindowFailureForTesting=null;
                if(child!=null)CloseTab(main,child);popup.Close();
                await service.Remove(home.View.CoreWebView2.Profile);await main.ChangeProtonPreview(false);
            }
        }
    }
}

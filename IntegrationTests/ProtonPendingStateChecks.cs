using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;
using HarborBrowser;
using HarborBrowser.Services;
using HarborBrowser.Views;
using Microsoft.Web.WebView2.Core;

internal static partial class Program
{
    // No login, fork message, selector, account URL, or real pending state is used.
    // Every storage operation names one of this invocation's three random canaries.
    private static async Task ProtonPendingStateChecks(MainWindow main,BrowserDataStore store,string output)
    {
        var service=(ProtonPreviewService)Field(main,"_protonPreview");
        var original=(BrowserTab)((TabItem)main.BrowserTabs.SelectedItem).Tag;
        var mainCore=original.View.CoreWebView2;
        string token=Guid.NewGuid().ToString("N");
        string localKey="harbor-pending-local-"+token;
        string rawForkKey="fharbor-synthetic-pending-"+token;
        string sessionKey="harbor-pending-session-"+token;
        var evidence=new Dictionary<string,Dictionary<string,bool>>();
        bool reloadCanarySupported=false;
        ProtonPreviewWindow popup=null;CoreWebView2Controller second=null;BrowserTab newTab=null;
        CoreWebView2 cleanupCore=null;
        string diagnostic=Path.Combine(output,"pending-storage-booleans.json");
        var registrations=(await mainCore.Profile.GetBrowserExtensionsAsync()).ToDictionary(e=>e.Id,e=>e.IsEnabled);
        Check("pending-state diagnostic uses the explicit isolated test store",store.RootFolder==Path.GetFullPath(Path.Combine(output,"Profile")));
        Check("pending-state diagnostic starts with provider off",!service.Enabled&&!registrations.ContainsKey(ProtonPreviewService.ExtensionId));
        try{
            await main.ChangeProtonPreview(true);
            popup=main.OpenProtonPreview();Offscreen(popup);
            await Wait(()=>popup.ControlsReady||popup.ControlsError!=null);
            Check("pending-state fixture opens unmodified signed-out provider controls",popup.ControlsReady);
            var provider=popup.ProviderView.CoreWebView2;cleanupCore=provider;
            Check("provider popup shares the normal test profile",provider.Profile.ProfileName==mainCore.Profile.ProfileName&&!provider.Profile.IsInPrivateModeEnabled);

            var written=await PendingCanaryOperation(provider,localKey,rawForkKey,sessionKey,"write");
            evidence["initialWrite"]=written;
            evidence["scopeSummary"]=new Dictionary<string,bool>{
                {"testApiSupported",!written["localSetError"]&&!written["localGetError"]&&!written["nativePromiseSetError"]&&!written["providerGetError"]&&written["localPresent"]&&written["providerPresent"]},
                {"scopeProbed",false},{"scopeStable",false},
                {"reloadDetectionSupported",written["sessionApi"]&&!written["sessionSetError"]&&!written["sessionGetError"]&&written["sessionPresent"]},
                {"reloadSensitiveCanaryStable",false}
            };
            File.WriteAllText(diagnostic,Json.Serialize(evidence));
            Check("canary replies contain booleans from the expected provider page",written["providerIdentity"]&&!written["scriptError"]);
            Check("Chrome local callback write and exact-key read succeed",!written["localSetError"]&&!written["localGetError"]&&written["localPresent"]);
            Check("provider-style local write reports no API failure",!written["nativePromiseSetError"]);
            Check("provider-style canary is in Chrome local storage",!written["providerGetError"]&&written["providerPresent"]);
            Check("provider-style write needed neither page-local nor global-memory fallback",!written["fallbackLocalUsed"]&&!written["fallbackMemoryUsed"]&&!written["fallbackLocalPresent"]&&!written["fallbackMemoryPresent"]);
            reloadCanarySupported=written["sessionApi"]&&!written["sessionSetError"]&&!written["sessionGetError"]&&written["sessionPresent"];
            if(reloadCanarySupported)Check("native session canary is available to detect extension reload",true);
            else Console.WriteLine("UNVERIFIED native session canary is unavailable; local continuity checks continue, but extension-reload detection is inconclusive.");

            var environment=(CoreWebView2Environment)Field(main,"_environment");
            var options=environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName=provider.Profile.ProfileName;options.IsInPrivateModeEnabled=false;
            var creating=environment.CreateCoreWebView2ControllerAsync(new WindowInteropHelper(main).Handle,options);
            if(await Task.WhenAny(creating,Task.Delay(20000))!=creating){_ = ClosePendingCanaryController(creating);throw new TimeoutException("Synthetic storage controller did not start.");}
            second=await creating;second.IsVisible=false;
            var secondCore=second.CoreWebView2;
            var loaded=NextLoad(secondCore,ProtonPreviewService.PopupUrl);secondCore.Navigate(ProtonPreviewService.PopupUrl);await loaded;
            var crossed=await PendingCanaryOperation(secondCore,localKey,rawForkKey,sessionKey,"read");
            evidence["secondController"]=crossed;File.WriteAllText(diagnostic,Json.Serialize(evidence));
            Check("second extension controller shares the intended profile",secondCore.Profile.ProfileName==provider.Profile.ProfileName);
            Check("callback-written local canary crosses extension controllers",crossed["localPresent"]&&!crossed["localGetError"]);
            Check("provider-style local canary crosses extension controllers",crossed["providerPresent"]&&!crossed["providerGetError"]);
            if(reloadCanarySupported)Check("session canary crosses extension controllers",crossed["sessionPresent"]&&!crossed["sessionGetError"]);

            popup.Close();popup=null;cleanupCore=secondCore;
            popup=main.OpenProtonPreview();Offscreen(popup);await Wait(()=>popup.ControlsReady||popup.ControlsError!=null);
            Check("provider controls reopen for pending-state continuity",popup.ControlsReady);
            provider=popup.ProviderView.CoreWebView2;cleanupCore=provider;
            var reopened=await PendingCanaryOperation(provider,localKey,rawForkKey,sessionKey,"read");
            evidence["reopenedPopup"]=reopened;File.WriteAllText(diagnostic,Json.Serialize(evidence));
            Check("both local canaries survive closing and reopening the popup",reopened["localPresent"]&&reopened["providerPresent"]&&!reopened["localGetError"]&&!reopened["providerGetError"]);
            if(reloadCanarySupported)Check("reopening the popup does not reload extension session storage",reopened["sessionPresent"]&&!reopened["sessionGetError"]);

            newTab=AddTab(main,HomePageService.Url);main.BrowserTabs.SelectedItem=newTab.Item;
            await Wait(()=>newTab.Initialized);
            await service.Prepare(newTab.View.CoreWebView2.Profile);
            var prepared=await PendingCanaryOperation(provider,localKey,rawForkKey,sessionKey,"read");
            evidence["newNormalTabAndPrepare"]=prepared;File.WriteAllText(diagnostic,Json.Serialize(evidence));
            Check("a new normal tab uses the same provider profile",newTab.View.CoreWebView2.Profile.ProfileName==provider.Profile.ProfileName);
            Check("new-tab preparation retains both pending-state canaries",prepared["localPresent"]&&prepared["providerPresent"]&&!prepared["localGetError"]&&!prepared["providerGetError"]);
            if(reloadCanarySupported)Check("new-tab preparation retains the reload-sensitive session canary",prepared["sessionPresent"]&&!prepared["sessionGetError"]);
            Check("no page-local or global fallback appeared during continuity checks",!prepared["fallbackLocalPresent"]&&!prepared["fallbackMemoryPresent"]);
            evidence["scopeSummary"]=new Dictionary<string,bool>{
                {"testApiSupported",!written["localSetError"]&&!written["localGetError"]&&!written["nativePromiseSetError"]&&!written["providerGetError"]},
                {"scopeProbed",true},
                {"scopeStable",crossed["localPresent"]&&crossed["providerPresent"]&&reopened["localPresent"]&&reopened["providerPresent"]&&prepared["localPresent"]&&prepared["providerPresent"]},
                {"reloadDetectionSupported",reloadCanarySupported},
                {"reloadSensitiveCanaryStable",reloadCanarySupported&&crossed["sessionPresent"]&&reopened["sessionPresent"]&&prepared["sessionPresent"]}
            };
            File.WriteAllText(diagnostic,Json.Serialize(evidence));

            var removed=await PendingCanaryOperation(provider,localKey,rawForkKey,sessionKey,"remove");
            evidence["exactCanaryCleanup"]=removed;File.WriteAllText(diagnostic,Json.Serialize(evidence));
            Check("cleanup removes only the explicitly named synthetic canaries",!removed["cleanupError"]&&!removed["localPresent"]&&!removed["providerPresent"]&&!removed["sessionPresent"]&&!removed["fallbackLocalPresent"]&&!removed["fallbackMemoryPresent"]);
        }finally{
            if(cleanupCore!=null)try{
                var cleanup=await PendingCanaryOperation(cleanupCore,localKey,rawForkKey,sessionKey,"remove");
                evidence["finallyExactCanaryCleanup"]=cleanup;File.WriteAllText(diagnostic,Json.Serialize(evidence));
            }catch{Console.WriteLine("UNVERIFIED exact canary callback cleanup; the isolated provider removal still follows.");}
            try{second?.Close();}catch{}
            popup?.Close();
            if(newTab!=null){CloseTab(main,newTab);main.BrowserTabs.SelectedItem=original.Item;}
            if(service.Enabled)await main.ChangeProtonPreview(false);
        }
        Check("pending-state diagnostic removes its own provider registration",!service.Enabled&&!(await mainCore.Profile.GetBrowserExtensionsAsync()).Any(e=>e.Id==ProtonPreviewService.ExtensionId));
        Check("pending-state diagnostic preserves unrelated native extensions",(await mainCore.Profile.GetBrowserExtensionsAsync()).All(e=>registrations.ContainsKey(e.Id)&&registrations[e.Id]==e.IsEnabled));
    }

    private static async Task ClosePendingCanaryController(Task<CoreWebView2Controller> creation)
    {try{(await creation).Close();}catch{}}

    private static async Task<Dictionary<string,bool>> PendingCanaryOperation(CoreWebView2 core,string localKey,string rawForkKey,string sessionKey,string operation)
    {
        if(!ProtonPreviewService.IsProviderPage(core.Source)||new Uri(core.Source).AbsolutePath!="/popup.html")throw new InvalidOperationException("Synthetic canary operation requires the actual Proton popup page.");
        string channel="harborCanary_"+Guid.NewGuid().ToString("N");
        var done=new TaskCompletionSource<Dictionary<string,bool>>();
        EventHandler<CoreWebView2WebMessageReceivedEventArgs> received=(s,e)=>{
            if(!ProtonPreviewService.IsProviderPage(e.Source)||new Uri(e.Source).AbsolutePath!="/popup.html")return;
            try{
                string json=e.WebMessageAsJson;
                if(json.Length>4096||!json.Contains("\""+channel+"\""))return;
                var values=Json.Deserialize<Dictionary<string,object>>(json);object nonce;
                if(values==null||!values.TryGetValue(channel,out nonce)||!(nonce is bool)||(bool)nonce!=true)return;
                if(values.Values.Any(v=>!(v is bool)))throw new InvalidDataException("Synthetic storage response included a non-boolean field.");
                done.TrySetResult(values.Where(p=>p.Key!=channel).ToDictionary(p=>p.Key,p=>(bool)p.Value));
            }catch(Exception ex){done.TrySetException(ex);}
        };
        string script=@"(async()=>{
            const channel=CHANNEL, localKey=LOCALKEY, rawKey=RAWKEY, sessionKey=SESSIONKEY, operation=OPERATION;
            const prefixedKey='bex-'+rawKey;
            const localArea=globalThis.chrome?.storage?.local,sessionArea=globalThis.chrome?.storage?.session;
            const result={providerIdentity:globalThis.chrome?.runtime?.id==='jplgfhpmjnbigmhklmmbgecoobifkmpa',scriptError:false,
                localSetError:false,localGetError:false,localPresent:false,nativePromiseSetError:false,promiseReturned:false,
                providerGetError:false,providerPresent:false,fallbackLocalUsed:false,fallbackMemoryUsed:false,
                fallbackLocalPresent:false,fallbackMemoryPresent:false,fallbackLocalAccessError:false,
                sessionApi:!!sessionArea,sessionSetError:false,sessionGetError:false,sessionPresent:false,cleanupError:false};
            const callback=(area,method,arg)=>new Promise(resolve=>{
                try{area[method](arg,value=>{const error=!!globalThis.chrome?.runtime?.lastError;resolve({error,value});});}
                catch{resolve({error:true,value:undefined});}
            });
            try{
                if(operation==='write'){
                    const first=await callback(localArea,'set',{[localKey]:{harborSynthetic:true}});
                    result.localSetError=first.error;
                    // Mirror Proton storage.ts's await/set/fallback behavior for this own key only.
                    try{
                        const pending=localArea.set({[prefixedKey]:{harborSynthetic:true}});
                        result.promiseReturned=!!pending&&typeof pending.then==='function';await pending;
                    }catch{
                        result.nativePromiseSetError=true;
                        try{localStorage.setItem(prefixedKey,JSON.stringify({harborSynthetic:true}));result.fallbackLocalUsed=true;}
                        catch{(globalThis.bexStorage||(globalThis.bexStorage={}))[rawKey]={harborSynthetic:true};result.fallbackMemoryUsed=true;}
                    }
                    if(result.sessionApi){result.sessionSetError=(await callback(sessionArea,'set',{[sessionKey]:{harborSynthetic:true}})).error;}
                    else result.sessionSetError=true;
                }
                if(operation==='remove'){
                    result.cleanupError=(await callback(localArea,'remove',[localKey,prefixedKey])).error;
                    if(result.sessionApi)result.cleanupError=(await callback(sessionArea,'remove',sessionKey)).error||result.cleanupError;
                    try{localStorage.removeItem(prefixedKey);}catch{result.cleanupError=true;}
                    if(globalThis.bexStorage&&Object.prototype.hasOwnProperty.call(globalThis.bexStorage,rawKey))delete globalThis.bexStorage[rawKey];
                }
                const local=await callback(localArea,'get',localKey);
                result.localGetError=local.error;result.localPresent=!!local.value?.[localKey]?.harborSynthetic;
                const provider=await callback(localArea,'get',prefixedKey);
                result.providerGetError=provider.error;result.providerPresent=!!provider.value?.[prefixedKey]?.harborSynthetic;
                if(result.sessionApi){const session=await callback(sessionArea,'get',sessionKey);result.sessionGetError=session.error;result.sessionPresent=!!session.value?.[sessionKey]?.harborSynthetic;}
                try{result.fallbackLocalPresent=localStorage.getItem(prefixedKey)!==null;}catch{result.fallbackLocalAccessError=true;}
                result.fallbackMemoryPresent=!!globalThis.bexStorage&&Object.prototype.hasOwnProperty.call(globalThis.bexStorage,rawKey);
            }catch{result.scriptError=true;}
            chrome.webview.postMessage({[channel]:true,...result});
        })()";
        script=script.Replace("CHANNEL",Json.Serialize(channel)).Replace("LOCALKEY",Json.Serialize(localKey)).Replace("RAWKEY",Json.Serialize(rawForkKey)).Replace("SESSIONKEY",Json.Serialize(sessionKey)).Replace("OPERATION",Json.Serialize(operation));
        core.WebMessageReceived+=received;
        try{
            var execution=core.ExecuteScriptAsync(script);
            if(await Task.WhenAny(execution,Task.Delay(10000))!=execution){
                _ = execution.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("Synthetic storage script did not start; no state values were collected.");
            }
            await execution;
            if(await Task.WhenAny(done.Task,Task.Delay(10000))!=done.Task)throw new TimeoutException("Synthetic storage API callback timed out; no state values were collected.");
            return await done.Task;
        }finally{core.WebMessageReceived-=received;}
    }
}

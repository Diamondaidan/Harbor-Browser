using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using HarborBrowser.Services;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private readonly ConditionalWeakTable<CoreWebView2,object> _protonWorkerMonitors=new ConditionalWeakTable<CoreWebView2,object>();
        private readonly Dictionary<string,WeakReference<CoreWebView2>> _protonOrdinaryWorkers=new Dictionary<string,WeakReference<CoreWebView2>>();
        private readonly HashSet<string> _protonStoppingWorkers=new HashSet<string>();

        // Unloading a document does not stop a service worker's waitUntil task.
        // Stop ordinary website workers when pausing without unregistering them,
        // deleting site storage, or touching Proton's extension background worker.
        private async Task ConfigureProtonWorkerLifecycle(CoreWebView2 core)
        {
            object existing;if(_protonWorkerMonitors.TryGetValue(core,out existing))return;
            _protonWorkerMonitors.Add(core,new object());
            var receiver=core.GetDevToolsProtocolEventReceiver("ServiceWorker.workerVersionUpdated");
            receiver.DevToolsProtocolEventReceived+=(s,e)=>
            {
                try
                {
                    var data=new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=12}.Deserialize<Dictionary<string,object>>(e.ParameterObjectAsJson);
                    object list;if(data==null||!data.TryGetValue("versions",out list))return;
                    foreach(var version in ((IEnumerable)list).OfType<Dictionary<string,object>>())
                    {
                        object idValue,urlValue,statusValue;
                        if(!version.TryGetValue("versionId",out idValue)||!version.TryGetValue("scriptURL",out urlValue)||!version.TryGetValue("runningStatus",out statusValue))continue;
                        string id=idValue as string,url=urlValue as string,status=statusValue as string;
                        if(string.IsNullOrEmpty(id)||id.Length>128||!ProtonProtectionState.IsOrdinaryRequest(url,false))continue;
                        if(status=="stopped")_protonOrdinaryWorkers.Remove(id);
                        else
                        {
                            _protonOrdinaryWorkers[id]=new WeakReference<CoreWebView2>(core);
                            if(ProtonProtection.Enabled&&ProtonProtection.State!=ProtonProtectionPhase.Protected)_=StopProtonWorker(id,core);
                        }
                    }
                }
                catch(Exception){ /* No worker metadata is logged or persisted. */ }
            };
            await core.CallDevToolsProtocolMethodAsync("ServiceWorker.enable","{}");
        }

        private void StopOrdinaryProtonWorkers()
        {
            foreach(var worker in _protonOrdinaryWorkers.ToArray())
            {
                CoreWebView2 core;
                if(worker.Value.TryGetTarget(out core))_=StopProtonWorker(worker.Key,core);
                else _protonOrdinaryWorkers.Remove(worker.Key);
            }
        }

        private async Task StopProtonWorker(string id,CoreWebView2 core)
        {
            if(!_protonStoppingWorkers.Add(id))return;
            try
            {
                await ProtonRouteProbe.Bounded(core.CallDevToolsProtocolMethodAsync("ServiceWorker.stopWorker",
                    new JavaScriptSerializer().Serialize(new{versionId=id})),5000,_protonRequestLifetime.Token);
            }
            catch(Exception)
            {
                _protonOrdinaryWorkers.Remove(id);
                if(!_shuttingDown&&ProtonProtection.Enabled)SetStatus("Browsing is paused. A website background worker could not be stopped; browser protection cannot guarantee every background connection has stopped.");
            }
            finally{_protonStoppingWorkers.Remove(id);}
        }
    }
}

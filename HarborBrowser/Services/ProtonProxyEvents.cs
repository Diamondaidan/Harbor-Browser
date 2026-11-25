using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    internal enum ProtonProxyEvent { Settings, Privacy, FailedTransaction, UnsafeError }

    internal static class ProtonProxyEvents
    {
        internal static string WatchScript(string nonce)
        {
            return @"(()=>{
                if(location.protocol!=='chrome-extension:'||location.hostname!=='jplgfhpmjnbigmhklmmbgecoobifkmpa'||window.__harborProtectionWatcher)return;
                window.__harborProtectionWatcher=true;
                const changed=(kind,fatal)=>chrome.webview.postMessage({harborProxyChanged:"+new JavaScriptSerializer().Serialize(nonce)+@",kind,fatal:!!fatal});
                chrome.proxy.settings.onChange.addListener(()=>changed('settings',false));
                chrome.proxy.onProxyError?.addListener(d=>changed('proxy-error',d?.fatal===true));
                chrome.privacy?.network?.webRTCIPHandlingPolicy?.onChange?.addListener(()=>changed('privacy',false));
            })()";
        }

        // Only bounded categories and a transaction-aborted flag cross into
        // the host. No URL, proxy address, error details or account data.
        internal static bool TryRead(string json,string nonce,out ProtonProxyEvent result)
        {
            result=ProtonProxyEvent.UnsafeError;
            if(string.IsNullOrEmpty(json)||json.Length>1024)return false;
            try
            {
                var data=new JavaScriptSerializer{MaxJsonLength=1024,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(json);
                object key,kind,fatal;
                if(data==null||!data.TryGetValue("harborProxyChanged",out key)||!(key is string)||(string)key!=nonce||!data.TryGetValue("kind",out kind))return false;
                switch(kind as string)
                {
                    case "settings":result=ProtonProxyEvent.Settings;return true;
                    case "privacy":result=ProtonProxyEvent.Privacy;return true;
                    case "proxy-error":result=data.TryGetValue("fatal",out fatal)&&fatal is bool&&(bool)fatal?ProtonProxyEvent.FailedTransaction:ProtonProxyEvent.UnsafeError;return true;
                    default:return false;
                }
            }
            catch{return false;}
        }
    }
}

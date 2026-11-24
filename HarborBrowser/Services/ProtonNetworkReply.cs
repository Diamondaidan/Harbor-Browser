using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Services
{
    // Original network bytes from the dedicated verifier renderer, independent
    // of DOM changes. No account pages, response headers or addresses are logged.
    internal sealed class ProtonNetworkReply
    {
        private readonly CoreWebView2 _core;
        private readonly string _target;
        private readonly TaskCompletionSource<string> _finished=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CoreWebView2DevToolsProtocolEventReceiver _response,_complete,_failed;
        private string _request;
        private bool _closed;
        internal ProtonNetworkReply(CoreWebView2 core,string target)
        {
            if(!ProtonRouteProbe.IsAddressTarget(target))
                throw new InvalidOperationException("The verifier destination is not permitted.");
            _core=core;_target=target;
            _response=core.GetDevToolsProtocolEventReceiver("Network.responseReceived");
            _complete=core.GetDevToolsProtocolEventReceiver("Network.loadingFinished");
            _failed=core.GetDevToolsProtocolEventReceiver("Network.loadingFailed");
            _response.DevToolsProtocolEventReceived+=Response;
            _complete.DevToolsProtocolEventReceived+=Complete;
            _failed.DevToolsProtocolEventReceived+=Failed;
        }
        internal async Task Prepare(CancellationToken cancellation)
        {
            await _core.CallDevToolsProtocolMethodAsync("Network.enable","{\"maxTotalBufferSize\":65536,\"maxResourceBufferSize\":16384,\"maxPostDataSize\":0}");
            cancellation.ThrowIfCancellationRequested();
            await _core.CallDevToolsProtocolMethodAsync("Network.setBypassServiceWorker","{\"bypass\":true}");
            cancellation.ThrowIfCancellationRequested();
            await _core.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled","{\"cacheDisabled\":true}");
            cancellation.ThrowIfCancellationRequested();
        }
        private static Dictionary<string,object> Event(string json)
        {
            if(json==null||json.Length>65536)throw new InvalidDataException("The verifier event was too large.");
            return new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=16}.Deserialize<Dictionary<string,object>>(json);
        }
        private void Response(object sender,CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
        {
            if(_closed)return;
            try
            {
                var data=Event(args.ParameterObjectAsJson);object value,id,type,url,status,cached;
                if(data==null||!data.TryGetValue("response",out value))return;
                var response=value as Dictionary<string,object>;
                if(response==null||!response.TryGetValue("url",out url)||!Equals(url,_target))return;
                if(!data.TryGetValue("type",out type)||!Equals(type,"Document")||!data.TryGetValue("requestId",out id)||!(id is string)||((string)id).Length>128||((string)id).Length==0||
                    !response.TryGetValue("status",out status)||Convert.ToInt32(status)!=200)
                    throw new InvalidDataException("The verifier did not return a successful document.");
                foreach(string flag in new[]{"fromServiceWorker","fromDiskCache","fromPrefetchCache"})
                    if(response.TryGetValue(flag,out cached)&&Equals(cached,true))throw new InvalidDataException("The verifier reply was not a fresh network response.");
                _request=(string)id;
            }
            catch { _finished.TrySetException(new InvalidDataException("The verifier's network reply could not be confirmed.")); }
        }
        private void Complete(object sender,CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
        {
            if(_closed||_request==null)return;
            try{var data=Event(args.ParameterObjectAsJson);object id;if(data!=null&&data.TryGetValue("requestId",out id)&&Equals(id,_request))_finished.TrySetResult(_request);}
            catch{_finished.TrySetException(new InvalidDataException("The verifier's completed reply could not be confirmed."));}
        }
        private void Failed(object sender,CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
        {
            if(_closed||_request==null)return;
            try{var data=Event(args.ParameterObjectAsJson);object id;if(data!=null&&data.TryGetValue("requestId",out id)&&Equals(id,_request))_finished.TrySetException(new InvalidDataException("The verifier's network request failed."));}
            catch{_finished.TrySetException(new InvalidDataException("The verifier's network request failed."));}
        }
        internal async Task<byte[]> Read(CancellationToken cancellation)
        {
            string id=await _finished.Task;cancellation.ThrowIfCancellationRequested();
            string json=await _core.CallDevToolsProtocolMethodAsync("Network.getResponseBody",new JavaScriptSerializer().Serialize(new{requestId=id}));
            cancellation.ThrowIfCancellationRequested();
            if(json==null||json.Length>100000)throw new InvalidDataException("The verifier reply was too large.");
            try
            {
                var value=new JavaScriptSerializer{MaxJsonLength=100000,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(json);object body,encoded;
                if(value==null||!value.TryGetValue("body",out body)||!(body is string)||!value.TryGetValue("base64Encoded",out encoded)||!(encoded is bool))throw new InvalidDataException();
                byte[] bytes=(bool)encoded?Convert.FromBase64String((string)body):Encoding.UTF8.GetBytes((string)body);
                if(bytes.Length>16384)throw new InvalidDataException();
                return bytes;
            }
            catch{throw new InvalidDataException("The verifier returned no bounded network content.");}
        }
        internal async Task Close()
        {
            if(_closed)return;_closed=true;
            _finished.TrySetCanceled();
            try{_response.DevToolsProtocolEventReceived-=Response;_complete.DevToolsProtocolEventReceived-=Complete;_failed.DevToolsProtocolEventReceived-=Failed;}catch{}
            _=_finished.Task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            try{await ProtonRouteProbe.Bounded(_core.CallDevToolsProtocolMethodAsync("Network.disable","{}"),1000,CancellationToken.None);}catch{}
        }
    }
}

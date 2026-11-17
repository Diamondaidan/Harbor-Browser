using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HarborBrowser.Updates
{
    internal interface IUpdateTransport { Task<UpdateResponse> Open(Uri url,long offset,string etag,CancellationToken cancel); }
    internal sealed class UpdateResponse : IDisposable
    {
        internal int Status; internal string ETag,ContentRange,Encoding; internal long Length;
        internal Stream Body; internal Action Cleanup;
        public void Dispose(){try{Body?.Dispose();}finally{Cleanup?.Invoke();}}
    }
    internal sealed class GitHubUpdateTransport : IUpdateTransport
    {
        private readonly Func<bool> _network; private readonly Func<Uri,bool> _allowed;
        internal GitHubUpdateTransport(Func<bool> network,Func<Uri,bool> allowed=null){_network=network;_allowed=allowed??Allowed;}
        internal static bool Allowed(Uri u)
        {
            return u!=null&&u.IsAbsoluteUri&&u.Scheme=="https"&&u.Port==443&&u.UserInfo==""&&u.Fragment==""&&
                (u.Host=="github.com"||u.Host=="release-assets.githubusercontent.com"||u.Host=="objects.githubusercontent.com");
        }
        public async Task<UpdateResponse> Open(Uri url,long offset,string etag,CancellationToken cancel)
        {
            for(int redirects=0;redirects<=5;redirects++)
            {
                cancel.ThrowIfCancellationRequested();if(!_network())throw new InvalidOperationException("Updates are paused while a browser connection is enabled.");
                if(!_allowed(url))throw new InvalidDataException("An unexpected update download address was refused.");
                var request=(HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;request.Credentials=null;request.CookieContainer=null;
                request.UserAgent="Harbor-Updater/"+typeof(UpdateTrust).Assembly.GetName().Version.ToString(3);request.Timeout=20000;request.ReadWriteTimeout=20000;
                request.Headers[HttpRequestHeader.AcceptEncoding]="identity";request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                if(offset>0){request.AddRange(offset);request.Headers[HttpRequestHeader.IfRange]=etag;}
                var abort=cancel.Register(()=>request.Abort());HttpWebResponse response=null;
                try
                {
                    response=(HttpWebResponse)await request.GetResponseAsync();
                    int status=(int)response.StatusCode;
                    if(status==301||status==302||status==303||status==307||status==308)
                    {
                        Uri next;if(redirects==5||!Uri.TryCreate(url,response.Headers[HttpResponseHeader.Location],out next))throw new InvalidDataException("Too many or invalid update redirects.");
                        url=next;response.Dispose();abort.Dispose();continue;
                    }
                    var held=response;var registration=abort;
                    return new UpdateResponse{Status=status,ETag=response.Headers[HttpResponseHeader.ETag],ContentRange=response.Headers[HttpResponseHeader.ContentRange],Encoding=response.ContentEncoding,Length=response.ContentLength,Body=response.GetResponseStream(),Cleanup=()=>{held.Dispose();registration.Dispose();}};
                }
                catch(WebException ex)
                {
                    response?.Dispose();(ex.Response as IDisposable)?.Dispose();abort.Dispose();cancel.ThrowIfCancellationRequested();
                    throw new IOException("The public GitHub release could not be reached. Check the connection or try again later.");
                }
                catch{response?.Dispose();abort.Dispose();throw;}
            }
            throw new InvalidDataException("Too many update redirects.");
        }
    }
}

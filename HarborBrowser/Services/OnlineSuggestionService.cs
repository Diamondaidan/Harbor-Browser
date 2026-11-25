using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    public sealed class OnlineSuggestionService
    {
        private static readonly HttpClient Client=new HttpClient(new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false,UseDefaultCredentials=false}){Timeout=TimeSpan.FromSeconds(3)};
        private readonly Func<Uri,CancellationToken,Task<string>> transport;
        public OnlineSuggestionService(Func<Uri,CancellationToken,Task<string>> fetch=null){transport=fetch??Fetch;}
        public static bool IsSearchQuery(string query)
        {
            string text=(query??string.Empty).Trim();IPAddress address;
            return text.Length>=2&&text.Length<=160&&!text.Any(char.IsControl)&&text.IndexOfAny(new[]{'/','\\','@','?','#','=',':'})<0&&!(text.IndexOf(' ')<0&&(text.Contains(".")||text.Equals("localhost",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(text,out address)));
        }
        public static Uri Endpoint(string engine,string query)
        {
            string q=Uri.EscapeDataString(query);
            switch(engine)
            {
                case "google":return new Uri("https://suggestqueries.google.com/complete/search?client=firefox&q="+q);
                case "bing":return new Uri("https://www.bing.com/osjson.aspx?query="+q);
                case "brave":return new Uri("https://search.brave.com/api/suggest?q="+q);
                case "startpage":return new Uri("https://www.startpage.com/osuggestions?q="+q);
                case "duckduckgo":return new Uri("https://duckduckgo.com/ac/?type=list&q="+q);
                default:return null;
            }
        }
        public async Task<List<string>> GetAsync(string query,string engine,bool enabled,bool isPrivate,CancellationToken cancellation)
        {
            if(!enabled||isPrivate||!IsSearchQuery(query))return new List<string>();
            var endpoint=Endpoint(engine,query.Trim());if(endpoint==null)return new List<string>();
            try{using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){timeout.CancelAfter(TimeSpan.FromSeconds(3));return Parse(await transport(endpoint,timeout.Token).ConfigureAwait(false));}}
            catch(OperationCanceledException){return new List<string>();}catch(HttpRequestException){return new List<string>();}catch(IOException){return new List<string>();}catch(ArgumentException){return new List<string>();}catch(InvalidOperationException){return new List<string>();}
        }
        public static List<string> Parse(string json)
        {
            if(string.IsNullOrEmpty(json)||json.Length>65536)return new List<string>();
            var root=new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=12}.DeserializeObject(json) as object[];
            if(root==null||root.Length<2)return new List<string>();
            var values=root[1] as IEnumerable;if(values==null||values is string)return new List<string>();
            var result=new List<string>();
            foreach(object value in values)
            {
                string text=value as string;var array=value as object[];if(text==null&&array!=null&&array.Length>0)text=array[0] as string;
                if(string.IsNullOrWhiteSpace(text)||text.Length>240||text.Any(char.IsControl))continue;
                text=text.Trim();if(!result.Contains(text,StringComparer.OrdinalIgnoreCase))result.Add(text);if(result.Count==6)break;
            }
            return result;
        }
        private static async Task<string> Fetch(Uri uri,CancellationToken cancellation)
        {
            using(var response=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength.GetValueOrDefault()>65536)return string.Empty;
                using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using(var data=new MemoryStream())
                {
                    byte[] buffer=new byte[4096];int count;while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancellation).ConfigureAwait(false))>0){if(data.Length+count>65536)return string.Empty;data.Write(buffer,0,count);}return Encoding.UTF8.GetString(data.ToArray());
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace HarborBrowser.Services
{
    public static class VideoScriptService
    {
        public static string Create(string channel)
        {
            if(string.IsNullOrEmpty(channel)||channel.Length>80)throw new ArgumentException("Invalid media channel.");
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HarborBrowser.Assets.Media.media.js"))
            using(var reader=new StreamReader(stream))return reader.ReadToEnd().Replace("__HARBOR_CHANNEL__",channel);
        }
        public static string Command(string name)
        {
            if(name!="probe"&&name!="enter"&&name!="exit"&&name!="togglePlayback"&&name!="toggleMute")throw new ArgumentException("Unknown video command.");
            return "window.__harborMediaController ? window.__harborMediaController."+name+"() : null";
        }
        public static long Score(string json)
        {
            try{var probe=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(json);return probe!=null&&probe.ContainsKey("found")&&Equals(probe["found"],true)?Convert.ToInt64(probe["score"]):0;}
            catch{return 0;}
        }
    }
}

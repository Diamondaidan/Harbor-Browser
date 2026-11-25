using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace HarborBrowser.Services
{
    public static class BackgroundImageService
    {
        private static readonly Dictionary<string,string> Cache=new Dictionary<string,string>();
        public static string Import(string profile,string source)
        {
            var info=new FileInfo(source);if(info.Length>12*1024*1024)throw new ArgumentException("Choose an image smaller than 12 MB.");
            byte[] bytes=File.ReadAllBytes(source);int width,height;
            using(var stream=new MemoryStream(bytes))
            {
                var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];width=header.PixelWidth;height=header.PixelHeight;
                if(width<1||height<1||(long)width*height>24000000)throw new ArgumentException("Choose an image with fewer than 24 million pixels.");
            }
            BitmapImage image;
            using(var stream=new MemoryStream(bytes))
            {
                image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;
                if(Math.Max(width,height)>1600)image.DecodePixelWidth=Math.Max(1,(int)Math.Round(width*1600.0/Math.Max(width,height)));
                image.EndInit();image.Freeze();
            }
            var encoded=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(encoded);
            if(encoded.Length>8*1024*1024)throw new ArgumentException("Choose a smaller background image.");
            string directory=Path.Combine(profile,"HomeBackgrounds");Directory.CreateDirectory(directory);string name=Guid.NewGuid().ToString("N")+".png";File.WriteAllBytes(Path.Combine(directory,name),encoded.ToArray());encoded.Dispose();return name;
        }
        public static string ReadDataUri(string profile,string fileName)
        {
            if(string.IsNullOrEmpty(fileName)||fileName.Length!=36||Path.GetFileName(fileName)!=fileName||!fileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return null;
            Guid identifier;if(!Guid.TryParseExact(fileName.Substring(0,32),"N",out identifier))return null;
            string path=Path.Combine(profile,"HomeBackgrounds",fileName);
            try
            {
                var info=new FileInfo(path);if(!info.Exists||info.Length>8*1024*1024)return null;
                string key=path+"|"+info.LastWriteTimeUtc.Ticks+"|"+info.Length;
                lock(Cache)
                {
                    string cached;if(Cache.TryGetValue(key,out cached))return cached;
                    byte[] bytes=File.ReadAllBytes(path);if(bytes.Length<8||bytes[0]!=0x89||bytes[1]!=0x50||bytes[2]!=0x4e||bytes[3]!=0x47)return null;
                    string value="data:image/png;base64,"+Convert.ToBase64String(bytes);if(Cache.Count>=4)Cache.Clear();Cache[key]=value;return value;
                }
            }
            catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
        }
    }
}

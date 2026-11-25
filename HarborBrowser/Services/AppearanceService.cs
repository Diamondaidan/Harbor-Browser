using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using HarborBrowser.Models;

namespace HarborBrowser.Services
{
    public static class AppearanceService
    {
        public static void Normalize(BrowserSettings settings)
        {
            settings.ThemeMode = settings.ThemeMode == "Dark" ? "Dark" : "Light";
            if(settings.LightColors==null)settings.LightColors=new AppearanceColors();
            if(settings.DarkColors==null)settings.DarkColors=new AppearanceColors();
        }

        public static AppearanceColors Colors(BrowserSettings settings, string mode)
        {
            Normalize(settings);
            return mode=="Dark"?settings.DarkColors:settings.LightColors;
        }

        public static bool TryColor(string text, out Color color)
        {
            color=ColorsFallback();
            if(string.IsNullOrWhiteSpace(text))return false;
            string hex=text.Trim();
            if(!hex.StartsWith("#",StringComparison.Ordinal))hex="#"+hex;
            if(hex.Length==4)hex="#"+hex[1]+hex[1]+hex[2]+hex[2]+hex[3]+hex[3];
            if(hex.Length!=7)return false;
            uint rgb;
            if(!uint.TryParse(hex.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))return false;
            color=Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
            return true;
        }
        private static Color ColorsFallback(){return System.Windows.Media.Colors.Black;}
        public static string Hex(Color color){return string.Format("#{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B);}
        private static Color Read(string value,string fallback){Color c;return TryColor(value,out c)?c:(Color)ColorConverter.ConvertFromString(fallback);}
        public static Color Blend(Color first, Color second,double fraction)
        {
            return Color.FromRgb((byte)Math.Round(first.R+(second.R-first.R)*fraction),
                (byte)Math.Round(first.G+(second.G-first.G)*fraction),(byte)Math.Round(first.B+(second.B-first.B)*fraction));
        }
        private static double Linear(byte component)
        {
            double v=component/255.0;return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4);
        }
        public static double Luminance(Color c){return 0.2126*Linear(c.R)+0.7152*Linear(c.G)+0.0722*Linear(c.B);}
        public static double Contrast(Color a,Color b)
        {
            double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);
        }

        public static Dictionary<string,Color> Palette(BrowserSettings settings,string mode=null)
        {
            Normalize(settings);mode=mode??settings.ThemeMode;
            bool dark=mode=="Dark";AppearanceColors o=Colors(settings,mode);
            Color background=Read(o.Background,dark?"#101D27":"#F6F5F1");
            Color panel=Read(o.Panel,dark?"#182B38":"#EDF0F1");
            Color input=Read(o.Input,dark?"#203744":"#FFFFFF");
            Color text=Read(o.Text,dark?"#F0F1EC":"#193340");
            Color accent=Read(o.Accent,dark?"#EEB273":"#A95627");
            Color white=System.Windows.Media.Colors.White,black=Color.FromRgb(8,19,26);
            return new Dictionary<string,Color>{
                {"HarborWindow",background},{"HarborPanel",panel},{"HarborInput",input},{"HarborText",text},
                {"HarborAccent",accent},{"HarborMuted",Blend(text,panel,0.28)},{"HarborLine",Blend(panel,text,0.22)},
                {"HarborHover",Blend(panel,accent,0.20)},{"HarborSelected",Blend(panel,accent,0.30)},
                {"HarborAccentText",Contrast(accent,white)>Contrast(accent,black)?white:black}
            };
        }

        public static void Apply(ResourceDictionary resources,BrowserSettings settings)
        {
            var palette=Palette(settings);
            foreach(var pair in palette)
            {
                var brush=new SolidColorBrush(pair.Value);brush.Freeze();resources[pair.Key]=brush;
            }
            resources[SystemColors.ControlBrushKey]=resources["HarborPanel"];
            resources[SystemColors.ControlTextBrushKey]=resources["HarborText"];
            resources[SystemColors.WindowBrushKey]=resources["HarborInput"];
            resources[SystemColors.WindowTextBrushKey]=resources["HarborText"];
            resources[SystemColors.MenuBrushKey]=resources["HarborPanel"];
            resources[SystemColors.MenuTextBrushKey]=resources["HarborText"];
            resources[SystemColors.HighlightBrushKey]=resources["HarborSelected"];
            resources[SystemColors.HighlightTextBrushKey]=resources["HarborText"];
            resources[SystemColors.InfoBrushKey]=resources["HarborPanel"];
            resources[SystemColors.InfoTextBrushKey]=resources["HarborText"];
        }
        public static void ApplyApplication(BrowserSettings settings)
        {
            if(Application.Current!=null)Apply(Application.Current.Resources,settings);
        }
        public static object HomeAppearance(BrowserSettings settings)
        {
            Normalize(settings);
            return new { mode=settings.ThemeMode=="Dark"?"dark":"light", light=HomePalette(settings,"Light"),dark=HomePalette(settings,"Dark") };
        }
        private static object HomePalette(BrowserSettings settings,string mode)
        {
            var p=Palette(settings,mode);
            return new { ink=Hex(p["HarborText"]),muted=Hex(p["HarborMuted"]),surface=Hex(p["HarborInput"]),
                soft=Hex(p["HarborPanel"]),accent=Hex(p["HarborAccent"]),accentText=Hex(p["HarborAccentText"]),
                background=Hex(p["HarborWindow"]),line=Hex(p["HarborLine"]) };
        }

        public static Color FromHsv(double hue,double saturation,double value)
        {
            hue=((hue%360)+360)%360;saturation=Math.Max(0,Math.Min(1,saturation));value=Math.Max(0,Math.Min(1,value));
            double c=value*saturation,x=c*(1-Math.Abs((hue/60)%2-1)),m=value-c;
            double r=0,g=0,b=0;
            if(hue<60){r=c;g=x;}else if(hue<120){r=x;g=c;}else if(hue<180){g=c;b=x;}
            else if(hue<240){g=x;b=c;}else if(hue<300){r=x;b=c;}else{r=c;b=x;}
            return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
        }
        public static void ToHsv(Color color,out double hue,out double saturation,out double value)
        {
            double r=color.R/255.0,g=color.G/255.0,b=color.B/255.0;
            double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
            value=max;saturation=max==0?0:d/max;
            hue=d==0?0:max==r?60*(((g-b)/d)%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
            if(hue<0)hue+=360;
        }
    }
}

namespace HarborBrowser.Models
{
    public sealed class AppearancePreset
    {
        public string Name {get;set;}
        public string ThemeMode {get;set;}
        public AppearanceColors LightColors {get;set;}
        public AppearanceColors DarkColors {get;set;}
        public override string ToString(){return Name??string.Empty;}
        public AppearancePreset Copy(){return new AppearancePreset{Name=Name,ThemeMode=ThemeMode,LightColors=LightColors==null?new AppearanceColors():LightColors.Copy(),DarkColors=DarkColors==null?new AppearanceColors():DarkColors.Copy()};}
    }
}

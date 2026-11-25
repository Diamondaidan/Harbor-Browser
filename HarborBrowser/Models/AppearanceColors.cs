namespace HarborBrowser.Models
{
    public sealed class AppearanceColors
    {
        public string Accent { get; set; }
        public string Background { get; set; }
        public string Panel { get; set; }
        public string Input { get; set; }
        public string Text { get; set; }

        public AppearanceColors Copy()
        {
            return new AppearanceColors { Accent=Accent, Background=Background, Panel=Panel, Input=Input, Text=Text };
        }
    }
}

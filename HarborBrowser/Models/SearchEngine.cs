namespace HarborBrowser.Models
{
    public sealed class SearchEngine
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SearchUrl { get; set; }

        public SearchEngine(string id, string name, string searchUrl)
        {
            Id = id;
            Name = name;
            SearchUrl = searchUrl;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

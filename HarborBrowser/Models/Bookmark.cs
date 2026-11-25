namespace HarborBrowser.Models
{
    public sealed class Bookmark
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Folder { get; set; }
        public Bookmark Copy(){return new Bookmark{Id=Id,Title=Title,Url=Url,Folder=Folder};}
    }
}

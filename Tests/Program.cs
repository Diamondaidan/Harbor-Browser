using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Xml.Linq;
using HarborBrowser.Services;

internal static partial class Program
{
    private static int count;
    private static string root;
    private static void Equal(string test, string expected, string actual)
    {
        if (expected != actual) throw new Exception(test + ": expected " + expected + ", actual " + actual);
        Console.WriteLine("PASS " + test); count++;
    }
    private static void Check(string test, bool value) { Equal(test, "True", value.ToString()); }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream));
    }
    private static string FileBytes(string name, params byte[] bytes)
    {
        string path = Path.Combine(root, name); File.WriteAllBytes(path, bytes); return path;
    }
    private static string Zip(string name, params string[] entries)
    {
        string path = Path.Combine(root, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            foreach (var entry in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(entry).Open()))
                    writer.Write(entry == "mimetype" ? "application/epub+zip" : "test");
        return path;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--probe-providers")return ProbeProviders();
        if(args.Length==2&&args[0]=="--crash-profile")return CrashProfile(args[1]);
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/HarborBrowser;component/Themes/Controls.xaml") });
            AppearanceService.ApplyApplication(new HarborBrowser.Models.BrowserSettings());
            root = args[0];
            Directory.CreateDirectory(root);
            string url = "https://example.invalid/7e0ce342-2669-445e-9984-b0ca280db995";
            Equal("header wins", "report.zip", DownloadFileNameService.Suggest("attachment; filename=\"report.zip\"", "browser.bin", url, "application/octet-stream"));
            Equal("UTF-8 filename", "café.zip", DownloadFileNameService.Suggest("attachment; filename*=UTF-8''caf%C3%A9.zip", null, url, null));
            Equal("RFC 5987 language", "café.zip", DownloadFileNameService.Suggest("attachment; filename*=UTF-8'en'caf%C3%A9.zip", null, url, null));
            Equal("literal percent filename", "100%20done.zip", DownloadFileNameService.Suggest("attachment; filename=\"100%20done.zip\"", null, url, null));
            Equal("WebView2 suggested name", "original.zip", DownloadFileNameService.Suggest(null, @"C:\Downloads\original.zip", url, null));
            Equal("URL fallback", "report.pdf", DownloadFileNameService.Suggest(null, null, "https://example.invalid/report.pdf?token=abc", null));
            Equal("MIME extension", "7e0ce342-2669-445e-9984-b0ca280db995.zip", DownloadFileNameService.Suggest(null, null, url, "application/zip; charset=binary"));
            Equal("generic MIME extension", "download.pdf", DownloadFileNameService.Suggest(null, "download.bin", null, "application/pdf"));
            Equal("explicit extension preserved", "report.docx", DownloadFileNameService.Suggest("attachment; filename=\"report.docx\"", null, url, "application/zip"));
            Equal("empty fallback", "download", DownloadFileNameService.Suggest(null, null, null, null));
            Equal("reserved CON", "_CON.zip", DownloadFileNameService.Clean("CON.zip"));
            Equal("reserved COM1", "_COM1.pdf", DownloadFileNameService.Clean("COM1.pdf"));
            Equal("reserved superscript", "_LPT¹.png", DownloadFileNameService.Clean("LPT¹.png"));
            Equal("non-device COM10", "COM10.zip", DownloadFileNameService.Clean("COM10.zip"));
            Equal("trailing dots and spaces", "report.zip", DownloadFileNameService.Clean(" report.zip.  "));
            Equal("traversal", "report.zip", DownloadFileNameService.Clean(@"..\folder\report.zip"));
            Equal("invalid characters", "a_b_.zip", DownloadFileNameService.Clean("a:b?.zip"));
            Check("long name bounded", DownloadFileNameService.Clean(new string('a', 250) + ".zip").Length <= 160);
            Check("long extension retained", DownloadFileNameService.Clean(new string('a', 250) + ".zip").EndsWith(".zip"));
            string[] reserved = { Path.Combine(root, "REPORT.ZIP") };
            Equal("case-insensitive reservation", "report (2).zip", DownloadFileNameService.MakeUnique(root, "report.zip", reserved));
            Directory.CreateDirectory(Path.Combine(root, "folder.zip"));
            Equal("directory collision", "folder (2).zip", DownloadFileNameService.MakeUnique(root, "folder.zip", null));
            FileBytes("exists.pdf", 1);
            Equal("existing file collision", "exists (2).pdf", DownloadFileNameService.MakeUnique(root, "exists.pdf", null));

            string original = Path.Combine(root, "uuid");
            File.Copy(args[1], original);
            string before = Hash(original);
            string fixedPath = DownloadFileNameService.RepairCompletedFile(original, null);
            Equal("actual downloaded ZIP extension", ".zip", Path.GetExtension(fixedPath));
            Equal("actual ZIP bytes preserved", before, Hash(fixedPath));
            Equal("repair is idempotent", fixedPath, DownloadFileNameService.RepairCompletedFile(fixedPath, null));
            using (var zip = ZipFile.OpenRead(fixedPath)) Check("actual archive still readable", zip.Entries.Count == 37);
            string pdf = FileBytes("pdf.bin", 0x25, 0x50, 0x44, 0x46, 0x2d, 0x31);
            FileBytes("pdf.pdf", 9);
            Equal("PDF recovery avoids overwrite", "pdf (2).pdf", Path.GetFileName(DownloadFileNameService.RepairCompletedFile(pdf, null)));
            Equal("PDF collision bytes preserved", "09", BitConverter.ToString(File.ReadAllBytes(Path.Combine(root, "pdf.pdf"))));
            string png = FileBytes("image", 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a);
            Equal("PNG signature", ".png", DownloadFileNameService.DetectExtension(png));
            string jpg = FileBytes("photo", 0xff, 0xd8, 0xff, 1);
            Equal("JPEG signature", ".jpg", DownloadFileNameService.DetectExtension(jpg));
            string unknown = FileBytes("unknown", 1, 2, 3);
            Equal("unknown data preserved", unknown, DownloadFileNameService.RepairCompletedFile(unknown, null));
            Equal("MZ alone not executable", null, DownloadFileNameService.DetectExtension(FileBytes("not-exe", 0x4d, 0x5a)));
            Equal("Office docx ZIP", ".docx", DownloadFileNameService.DetectExtension(Zip("office", "[Content_Types].xml", "word/document.xml")));
            Equal("Office xlsx ZIP", ".xlsx", DownloadFileNameService.DetectExtension(Zip("sheet", "[Content_Types].xml", "xl/workbook.xml")));
            Equal("Office pptx ZIP", ".pptx", DownloadFileNameService.DetectExtension(Zip("slides", "[Content_Types].xml", "ppt/presentation.xml")));
            Equal("EPUB ZIP", ".epub", DownloadFileNameService.DetectExtension(Zip("ebook", "mimetype")));
            Equal("PE executable", ".exe", DownloadFileNameService.DetectExtension(args[2]));
            Equal("PE DLL not executable", null, DownloadFileNameService.DetectExtension(args[3]));

            HomeChecks();
            NewTabAndAppearanceChecks();
            VideoWindowChecks();
            ResolutionChecks();
            EverydayChecks();
            CustomizationChecks();
            PrivacyChecks();
            Layout(args[4], 1320, 850);
            Layout(args[4], 800, 560);
            AppearanceService.ApplyApplication(new HarborBrowser.Models.BrowserSettings {ThemeMode="Dark"});
            Layout(args[4], 1320, 850, "-dark");
            Console.WriteLine("ALL PASSED: " + count + " checks. Layout PNGs are offline WPF renders, not desktop screenshots.");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }


    private static void HomeChecks()
    {
        Equal("home alias resolved", HomePageService.Url, SearchEngineService.ResolveInput("harbor://home", null));
        string normalized;
        Check("home settings accepted", SearchEngineService.TryNormalizeHomePage("harbor://home", out normalized));
        Equal("home settings canonical URL", HomePageService.Url, normalized);
        Check("custom home settings accepted", SearchEngineService.TryNormalizeHomePage("https://example.com/", out normalized));
        Equal("custom home preserved", "https://example.com/", normalized);
        Check("home origin accepted", HomePageService.IsHomeUrl(HomePageService.Url));
        Check("home lookalike rejected", !HomePageService.IsHomeUrl("https://harbor.invalid.attacker.example/"));
        Check("home subdomain rejected", !HomePageService.IsHomeUrl("https://evil.harbor.invalid/"));
        Check("home HTTP rejected", !HomePageService.IsHomeUrl("http://harbor.invalid/"));
        Check("home port rejected", !HomePageService.IsHomeUrl("https://harbor.invalid:444/"));
        Check("home script path rejected", !HomePageService.IsHomeUrl("https://harbor.invalid/home.js"));
        Check("home userinfo rejected", !HomePageService.IsHomeUrl("https://user@harbor.invalid/"));
        Equal("trusted search message", "birds & sky", HomePageService.ReadSearchMessage(HomePageService.Url, "{\"type\":\"home-search\",\"value\":\" birds & sky \"}"));
        Equal("external message rejected", null, HomePageService.ReadSearchMessage("https://example.com/", "{\"type\":\"home-search\",\"value\":\"birds\"}"));
        Equal("malformed message rejected", null, HomePageService.ReadSearchMessage(HomePageService.Url, "not json"));
        Equal("non-string query rejected", null, HomePageService.ReadSearchMessage(HomePageService.Url, "{\"type\":\"home-search\",\"value\":{}}"));
        Equal("blank query rejected", null, HomePageService.ReadSearchMessage(HomePageService.Url, "{\"type\":\"home-search\",\"value\":\" \"}"));
        Equal("large query rejected", null, HomePageService.ReadSearchMessage(HomePageService.Url, "{\"type\":\"home-search\",\"value\":\"" + new string('x',4097) + "\"}"));
        Equal("default home set", HomePageService.Url, new HarborBrowser.Models.BrowserSettings().HomePage);
        var legacy = new HarborBrowser.Models.BrowserSettings {HomePage="https://duckduckgo.com/"};
        Check("legacy default migrated once", HomePageService.UpgradeDefaults(legacy));
        Equal("legacy default becomes Harbor", HomePageService.Url, legacy.HomePage);
        legacy.HomePage="https://duckduckgo.com/";
        Check("later choice not migrated again", !HomePageService.UpgradeDefaults(legacy));
        Equal("explicit DuckDuckGo choice preserved", "https://duckduckgo.com/", legacy.HomePage);
        var custom = new HarborBrowser.Models.BrowserSettings {HomePage="https://example.com/"};
        HomePageService.UpgradeDefaults(custom);
        Equal("custom upgrade home preserved", "https://example.com/", custom.HomePage);
        Check("migration marker survives copy", custom.Copy().HarborHomeIntroduced);
        var list = new System.Collections.Generic.List<HarborBrowser.Models.Bookmark>();
        list.Add(new HarborBrowser.Models.Bookmark {Title="Invalid",Url="javascript:alert(1)"});
        for (int i=0;i<20;i++) list.Add(new HarborBrowser.Models.Bookmark {Title="Bookmark " + i,Url="https://example.com/" + i});
        var json = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(
            HomePageService.CreateState(SearchEngineService.GetById("bing"), list, true)) as System.Collections.Generic.Dictionary<string,object>;
        Equal("selected search engine", "Bing", json["engineName"] as string);
        Check("private flag conveyed", (bool)json["isPrivate"]);
        Equal("home shortcuts limited to twelve", "12", ((object[])json["bookmarks"]).Length.ToString());
    }

    private static void Layout(string path, int width, int height, string suffix = "")
    {
        var doc = XDocument.Load(path);
        string[] events = { "Loaded", "Closing", "PreviewKeyDown", "StateChanged", "Click", "SelectionChanged", "KeyDown", "DragOver", "Drop", "DragLeave", "PreviewMouseLeftButtonDown", "PreviewMouseWheel", "GotKeyboardFocus", "PreviewMouseLeftButtonUp" };
        // Strip handlers only for an offline layout render; no browser/profile is instantiated.
        foreach (var attr in doc.Descendants().Attributes().Where(a =>
            (a.Name.NamespaceName == "http://schemas.microsoft.com/winfx/2006/xaml" && a.Name.LocalName == "Class") ||
            events.Contains(a.Name.LocalName)).ToList()) attr.Remove();
        var window = (Window)XamlReader.Parse(doc.ToString());
        Equal(width + " borderless", "None", window.WindowStyle.ToString());
        Check(width + " resize border", WindowChrome.GetWindowChrome(window).ResizeBorderThickness.Left == 6);
        var logo = (Image)window.FindName("HeaderBrandLogo");
        Check(width + " Beacon header logo loaded", logo != null && logo.Source != null && logo.Source.Width > 0);
        var minimize = (Button)window.FindName("MinimizeWindowButton");
        var maximize = (Button)window.FindName("MaximizeWindowButton");
        var close = (Button)window.FindName("CloseWindowButton");
        var gear = (Button)window.FindName("GearMenuButton");
        var address = (TextBox)window.FindName("AddressBox");
        var tabs = (TabControl)window.FindName("BrowserTabs");
        tabs.Items.Add(new TabItem { Header = "New tab", Width = 180 });
        tabs.Items.Add(new TabItem { Header = "Example", Width = 180 });
        address.Text = "https://example.com";
        var frame = (Border)window.Content;
        window.Content = null;
        frame.Measure(new Size(width, height));
        frame.Arrange(new Rect(0, 0, width, height));
        frame.UpdateLayout();
        var min = minimize.TransformToAncestor(frame).Transform(new Point());
        var max = maximize.TransformToAncestor(frame).Transform(new Point());
        var end = close.TransformToAncestor(frame).Transform(new Point());
        var cog = gear.TransformToAncestor(frame).Transform(new Point());
        var bar = address.TransformToAncestor(frame).Transform(new Point());
        Check(width + " caption buttons aligned", Math.Abs(min.Y - end.Y) < 1 && Math.Abs(max.Y - min.Y) < 1);
        Check(width + " tabs share caption row", Math.Abs(tabs.TransformToAncestor(frame).Transform(new Point()).Y-end.Y)<4);
        Check(width + " group bar removed", window.FindName("TabGroupFilterBox")==null);
        Check(width + " picture-in-picture toolbar exists", window.FindName("PictureInPictureButton")!=null);
        Check(width + " buttons ordered", min.X < max.X && max.X < end.X);
        Check(width + " close inside frame", end.X + close.ActualWidth <= width);
        Check(width + " cog moved below caption", cog.Y > end.Y + close.ActualHeight);
        Check(width + " cog in address row", Math.Abs(cog.Y - bar.Y) < 3);
        Check(width + " address still usable", address.ActualWidth > 200);
        Check(width + " caption buttons clickable", WindowChrome.GetIsHitTestVisibleInChrome(minimize) && WindowChrome.GetIsHitTestVisibleInChrome(maximize) && WindowChrome.GetIsHitTestVisibleInChrome(close));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(frame);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(root, "layout-" + width + suffix + ".png"))) encoder.Save(stream);
        window.Close();
    }
}

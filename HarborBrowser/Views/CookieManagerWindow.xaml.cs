using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace HarborBrowser.Views
{
    public partial class CookieManagerWindow : Window
    {
        private readonly CoreWebView2CookieManager _manager;
        private readonly string _uri;
        private readonly ObservableCollection<CookieRow> _rows = new ObservableCollection<CookieRow>();

        private sealed class CookieRow
        {
            public string Name { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
            public bool IsSecure { get; set; }
            public bool IsHttpOnly { get; set; }
            public string Lifetime { get; set; }
            public CoreWebView2Cookie Cookie { get; set; }
        }

        public CookieManagerWindow(CoreWebView2CookieManager manager, string uri)
        {
            InitializeComponent();
            HarborBrowser.Services.WindowFitService.Attach(this);
            _manager = manager;
            _uri = uri;
            ScopeText.Text = string.IsNullOrWhiteSpace(uri) ? "Showing cookies for the selected profile." : "Showing cookies available to " + uri;
            CookiesGrid.ItemsSource = _rows;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) { await LoadCookiesAsync(); }
        private async void Refresh_Click(object sender, RoutedEventArgs e) { await LoadCookiesAsync(); }

        private async Task LoadCookiesAsync()
        {
            try
            {
                _rows.Clear();
                var cookies = await _manager.GetCookiesAsync(_uri ?? string.Empty);
                foreach (CoreWebView2Cookie cookie in cookies.OrderBy(c => c.Domain).ThenBy(c => c.Name))
                {
                    _rows.Add(new CookieRow
                    {
                        Name = cookie.Name,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        IsSecure = cookie.IsSecure,
                        IsHttpOnly = cookie.IsHttpOnly,
                        Lifetime = cookie.IsSession ? "Session" : cookie.Expires.ToLocalTime().ToString("g"),
                        Cookie = cookie
                    });
                }
                Title = "Harbor Cookie Manager (" + _rows.Count + ")";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load cookies. " + ex.Message, "Cookie Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            CookieRow selected = CookiesGrid.SelectedItem as CookieRow;
            if (selected == null) return;
            try
            {
                _manager.DeleteCookie(selected.Cookie);
                await LoadCookiesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not remove the selected cookie. " + ex.Message, "Cookie Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Delete every cookie in this browser profile? This may sign you out of websites.",
                "Clear all cookies", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                _manager.DeleteAllCookies();
                await LoadCookiesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not clear cookies. " + ex.Message, "Cookie Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

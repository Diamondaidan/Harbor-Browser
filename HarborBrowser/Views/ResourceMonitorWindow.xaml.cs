using System;
using System.Windows;
using System.Windows.Threading;

namespace HarborBrowser.Views
{
    public partial class ResourceMonitorWindow : Window
    {
        private readonly Func<string> _reportProvider;
        private readonly DispatcherTimer _timer;

        public ResourceMonitorWindow(Func<string> reportProvider)
        {
            InitializeComponent();
            _reportProvider = reportProvider;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => RefreshReport();
            Loaded += (s, e) => { RefreshReport(); _timer.Start(); };
            Closed += (s, e) => _timer.Stop();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) { RefreshReport(); }

        private void Close_Click(object sender, RoutedEventArgs e) { _timer.Stop(); Close(); }

        private void RefreshReport()
        {
            try { ReportBox.Text = _reportProvider == null ? "No resource data available." : _reportProvider(); }
            catch (Exception ex) { ReportBox.Text = "Unable to read resource information: " + ex.Message; }
        }
    }
}

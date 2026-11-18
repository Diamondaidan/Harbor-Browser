using System;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Services;

namespace HarborBrowser.Views
{
    internal sealed class PageLoadingDetailsWindow : Window
    {
        internal readonly TextBox ReportBox;
        internal readonly Button CopyButton;
        internal PageLoadingDetailsWindow(string report)
        {
            Title = "Page loading details"; Width = 650; Height = 560; MinWidth = 330; MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(StyleProperty, typeof(Window)); WindowFitService.Attach(this);
            var layout = new DockPanel { Margin = new Thickness(16) }; Content = layout;
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
            CopyButton = new Button { Content = "Copy report", Margin = new Thickness(0, 10, 8, 0) };
            var close = new Button { Content = "Close", IsCancel = true, Margin = new Thickness(0, 10, 0, 0) };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
                Text = "This report contains error categories and version numbers. It excludes URLs and account data. Nothing is sent automatically." };
            DockPanel.SetDock(status, Dock.Top); layout.Children.Add(status);
            ReportBox = new TextBox { Text = report, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            System.Windows.Automation.AutomationProperties.SetName(ReportBox, "Local page loading report");
            layout.Children.Add(ReportBox); actions.Children.Add(CopyButton); actions.Children.Add(close);
            CopyButton.Click += (s, e) => { try { Clipboard.SetText(ReportBox.Text); status.Text = "Report copied. You choose where to share it."; } catch { status.Text = "The clipboard is busy. Select the report text and copy it manually."; } };
            close.Click += (s, e) => Close();
        }
    }
}

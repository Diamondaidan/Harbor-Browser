using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HarborBrowser.Views
{
    /// <summary>
    /// A ScrollViewer that eases mouse-wheel movement instead of jumping in fixed steps.
    /// Touch and scrollbar-thumb interaction continue to use the normal WPF behavior.
    /// </summary>
    public sealed class SmoothScrollViewer : ScrollViewer
    {
        private readonly DispatcherTimer _scrollTimer;
        private double _targetOffset;

        public SmoothScrollViewer()
        {
            _scrollTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _scrollTimer.Tick += ScrollTimer_Tick;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (!e.Handled && ScrollableHeight > 0)
            {
                if (!_scrollTimer.IsEnabled)
                    _targetOffset = VerticalOffset;

                _targetOffset = Math.Max(0, Math.Min(ScrollableHeight, _targetOffset - (e.Delta * 0.70)));
                _scrollTimer.Start();
                e.Handled = true;
                return;
            }

            base.OnPreviewMouseWheel(e);
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            double difference = _targetOffset - VerticalOffset;
            if (Math.Abs(difference) < 0.8)
            {
                ScrollToVerticalOffset(_targetOffset);
                _scrollTimer.Stop();
                return;
            }

            ScrollToVerticalOffset(VerticalOffset + (difference * 0.28));
        }
    }
}

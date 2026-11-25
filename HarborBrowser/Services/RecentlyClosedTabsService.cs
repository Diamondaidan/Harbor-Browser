using System;
using System.Collections.Generic;

namespace HarborBrowser.Services
{
    public sealed class ClosedTabSnapshot
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string GroupName { get; set; }
        public bool IsPinned { get; set; }
        public int Index { get; set; }
    }

    public sealed class RecentlyClosedTabsService
    {
        private readonly List<ClosedTabSnapshot> _tabs = new List<ClosedTabSnapshot>();
        public int Count { get { return _tabs.Count; } }
        public void Remember(ClosedTabSnapshot tab, bool isPrivate)
        {
            Uri uri;
            if (isPrivate || tab == null || !Uri.TryCreate(tab.Url, UriKind.Absolute, out uri) ||
                !(uri.Scheme == "http" || uri.Scheme == "https" || tab.Url == "about:blank" || tab.Url == "harbor://downloads")) return;
            _tabs.Add(new ClosedTabSnapshot { Url=tab.Url, Title=tab.Title, GroupName=tab.GroupName, IsPinned=tab.IsPinned, Index=tab.Index });
            if (_tabs.Count > 20) _tabs.RemoveAt(0);
        }
        public ClosedTabSnapshot TakeLast()
        {
            if (_tabs.Count == 0) return null;
            var tab = _tabs[_tabs.Count-1]; _tabs.RemoveAt(_tabs.Count-1); return tab;
        }
    }
}

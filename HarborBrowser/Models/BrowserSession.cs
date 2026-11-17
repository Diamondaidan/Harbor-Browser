using System;
using System.Collections.Generic;

namespace HarborBrowser.Models
{
    public sealed class BrowserSession
    {
        public List<BrowserSessionTab> Tabs { get; set; }
        public List<string> Groups { get; set; }
        public bool GroupsConfigured { get; set; }
        public int SelectedTabIndex { get; set; }
        public DateTime SavedAt { get; set; }

        public BrowserSession()
        {
            Tabs = new List<BrowserSessionTab>();
            Groups = new List<string>();
            GroupsConfigured = false;
            SelectedTabIndex = 0;
            SavedAt = DateTime.Now;
        }
    }

    public sealed class BrowserSessionTab
    {
        public string Url { get; set; }
        public bool IsPinned { get; set; }
        public string GroupName { get; set; }
    }
}

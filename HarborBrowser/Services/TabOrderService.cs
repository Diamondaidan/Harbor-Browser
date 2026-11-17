using System;
using System.Collections.Generic;
using System.Linq;

namespace HarborBrowser.Services
{
    public static class TabOrderService
    {
        public static int NewTabIndex(IList<bool> pinned, bool isPinned)
        {
            return isPinned ? pinned.Count(p => p) : pinned.Count;
        }

        // Returns the insertion index AFTER removing the source tab.
        public static int MoveIndex(IList<bool> pinned, int source, int target, bool after)
        {
            if (source < 0 || source >= pinned.Count || target < 0 || target >= pinned.Count) return source;
            if (source == target) return source;
            bool isPinned=pinned[source];
            int boundary=pinned.Count(p=>p)-(isPinned?1:0);
            int insertion=target+(after?1:0);
            if(source < insertion) insertion--;
            int minimum=isPinned?0:boundary;
            int maximum=isPinned?boundary:pinned.Count-1;
            return Math.Max(minimum, Math.Min(maximum,insertion));
        }
    }
}

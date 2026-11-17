using System;

namespace HarborBrowser.Services
{
    public static class TabStripLayout
    {
        public const double RegularWidth=190,MinimumWidth=54,PinnedWidth=43,NewButtonSpace=46;
        public static double Width(double viewport,int regular,int pinned)
        {
            if(regular<=0)return RegularWidth;
            double available=Math.Max(0,viewport-NewButtonSpace-pinned*PinnedWidth);
            return Math.Max(MinimumWidth,Math.Min(RegularWidth,Math.Floor(available/regular)));
        }
    }
}

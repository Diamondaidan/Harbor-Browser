namespace HarborBrowser.Services
{
    public static class ResponsiveLayout
    {
        public static bool ShowWordmark(double width){return width>=760;}
        public static bool ShowEngine(double width){return width>=900;}
        public static bool ShowSecondaryNavigation(double width){return width>=650;}
        public static double TabTitleWidth(double width){return width<600?90:150;}
    }
}

using System;
using System.Windows;
using System.Windows.Controls;

namespace HarborBrowser.Views
{
    public sealed class PictureInPictureWindow:Window
    {
        public ContentControl PlayerHost {get;private set;}
        public PictureInPictureWindow(Action returnToTab,Action playPause,Action mute)
        {
            Title="Picture in picture — Harbor";Width=500;Height=330;MinWidth=280;MinHeight=190;
            WindowStartupLocation=WindowStartupLocation.Manual;Topmost=true;ShowInTaskbar=false;
            var panel=new DockPanel();
            var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(5)};
            foreach(var pair in new[]{Tuple.Create("Play / pause",playPause),Tuple.Create("Mute / unmute",mute),Tuple.Create("Return to tab",returnToTab)})
            {var button=new Button{Content=pair.Item1,Padding=new Thickness(7,3,7,3),Margin=new Thickness(3,0,0,0)};button.Click+=(s,e)=>pair.Item2();controls.Children.Add(button);}
            DockPanel.SetDock(controls,Dock.Bottom);panel.Children.Add(controls);
            PlayerHost=new ContentControl();panel.Children.Add(PlayerHost);Content=panel;
        }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private string _activeGroupFilter="*";
        private MenuItem CreateTabGroupsMenu()
        {
            int visible=BrowserTabs.Items.Cast<TabItem>().Count(i=>i.Visibility==Visibility.Visible);
            var menu=CreateGearSubmenu("Tab groups ("+visible+" of "+BrowserTabs.Items.Count+" shown)");
            Action<string,string> filter=(label,value)=>{
                var item=CreateGearMenuItem(label);item.IsCheckable=true;item.IsChecked=_activeGroupFilter==value;
                item.Click+=(s,e)=>{RefreshGroupFilterBox(value);ApplyTabGroupFilter();};menu.Items.Add(item);
            };
            filter("Show all tabs","*");filter("Show ungrouped tabs","");
            foreach(string name in _tabGroups.OrderBy(g=>g,StringComparer.CurrentCultureIgnoreCase))filter("Show "+name,name);
            AddGearSeparator(menu);
            var move=CreateGearSubmenu("Move current tab to");
            AddGearCommand(move,"Ungrouped",(s,e)=>SetTabGroup(SelectedTab,null),SelectedTab!=null);
            foreach(string name in _tabGroups.ToArray())AddGearCommand(move,name,(s,e)=>SetTabGroup(SelectedTab,name),SelectedTab!=null);
            menu.Items.Add(move);
            AddGearCommand(menu,"Add group…",(s,e)=>AddTabGroup());
            foreach(string name in _tabGroups.ToArray())
            {
                var manage=CreateGearSubmenu("Manage "+name);
                AddGearCommand(manage,"Rename…",(s,e)=>RenameTabGroup(name));
                AddGearCommand(manage,"Remove group",(s,e)=>RemoveTabGroup(name));menu.Items.Add(manage);
            }
            return menu;
        }
    }
}

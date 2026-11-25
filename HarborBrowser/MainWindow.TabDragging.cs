using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HarborBrowser.Services;
using HarborBrowser.Views;

namespace HarborBrowser
{
    public partial class MainWindow
    {
        private Point _tabDragStart;
        private BrowserTab _dragCandidate;
        private TabItem _dropIndicator;

        private static T TabAncestor<T>(DependencyObject source) where T:DependencyObject
        {
            while(source!=null)
            {
                var match=source as T;if(match!=null)return match;
                source=source is Visual || source is System.Windows.Media.Media3D.Visual3D
                    ?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
        private void Tab_PreviewMouseDown(object sender,MouseButtonEventArgs e)
        {
            _dragCandidate=null;
            if(TabAncestor<Button>(e.OriginalSource as DependencyObject)!=null)return;
            var item=sender as TabItem;
            if(item==null)return;
            _dragCandidate=item.Tag as BrowserTab;_tabDragStart=e.GetPosition(BrowserTabs);
        }
        private void Tab_PreviewMouseMove(object sender,MouseEventArgs e)
        {
            if(_dragCandidate==null||e.LeftButton!=MouseButtonState.Pressed)return;
            var position=e.GetPosition(BrowserTabs);
            if(Math.Abs(position.X-_tabDragStart.X)<SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y-_tabDragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            BrowserTab tab=_dragCandidate;_dragCandidate=null;
            try { DragDrop.DoDragDrop(tab.Item,new DataObject(typeof(BrowserTab),tab),DragDropEffects.Move); }
            finally { ClearTabDropIndicator(); }
            e.Handled=true;
        }
        private BrowserTab DragTab(DragEventArgs e)
        {
            var tab=e.Data.GetDataPresent(typeof(BrowserTab))?e.Data.GetData(typeof(BrowserTab)) as BrowserTab:null;
            return tab!=null&&BrowserTabs.Items.Contains(tab.Item)?tab:null;
        }
        private void ClearTabDropIndicator()
        {
            if(_dropIndicator==null)return;
            _dropIndicator.ClearValue(Control.BorderBrushProperty);_dropIndicator.ClearValue(Control.BorderThicknessProperty);_dropIndicator=null;
        }
        private void BrowserTabs_DragOver(object sender,DragEventArgs e)
        {
            var tab=DragTab(e);e.Effects=tab==null?DragDropEffects.None:DragDropEffects.Move;e.Handled=true;
            ClearTabDropIndicator();if(tab==null)return;
            var target=TabAncestor<TabItem>(e.OriginalSource as DependencyObject);
            if(target!=null&&target!=tab.Item)
            {
                bool after=e.GetPosition(target).X>target.ActualWidth/2;
                _dropIndicator=target;
                target.SetResourceReference(Control.BorderBrushProperty,"HarborAccent");
                target.BorderThickness=after?new Thickness(0,0,3,0):new Thickness(3,0,0,0);
            }
            var scroll=FindTabScroller(BrowserTabs);
            if(scroll!=null)
            {
                double x=e.GetPosition(scroll).X;
                if(x<25)scroll.LineLeft();else if(x>scroll.ActualWidth-25)scroll.LineRight();
            }
        }
        private static ScrollViewer FindTabScroller(DependencyObject root)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            {
                var child=VisualTreeHelper.GetChild(root,i);
                var scroll=child as ScrollViewer;if(scroll!=null)return scroll;
                scroll=FindTabScroller(child);if(scroll!=null)return scroll;
            }
            return null;
        }
        private void BrowserTabs_DragLeave(object sender,DragEventArgs e){ClearTabDropIndicator();}
        private void BrowserTabs_Drop(object sender,DragEventArgs e)
        {
            ClearTabDropIndicator();BrowserTab tab=DragTab(e);if(tab==null)return;
            var target=TabAncestor<TabItem>(e.OriginalSource as DependencyObject);
            int targetIndex=target==null?BrowserTabs.Items.Count-1:BrowserTabs.Items.IndexOf(target);
            bool after=target==null||e.GetPosition(target).X>target.ActualWidth/2;
            MoveTabTo(tab,targetIndex,after);e.Handled=true;
        }
        private void MoveTabTo(BrowserTab tab,int targetIndex,bool after)
        {
            var pinned=BrowserTabs.Items.Cast<TabItem>().Select(i=>((BrowserTab)i.Tag).IsPinned).ToList();
            int source=BrowserTabs.Items.IndexOf(tab.Item);
            int index=TabOrderService.MoveIndex(pinned,source,targetIndex,after);
            if(index<0||index==source)return;
            var selected=BrowserTabs.SelectedItem;
            _reorderingTabs=true;
            try { BrowserTabs.Items.Remove(tab.Item);BrowserTabs.Items.Insert(index,tab.Item);BrowserTabs.SelectedItem=selected; }
            finally { _reorderingTabs=false; }
            ApplyTabGroupFilter();tab.Item.BringIntoView();SaveSession();SetStatus("Tab moved");
        }
        private void MoveTabRelative(BrowserTab tab,int direction)
        {
            if(tab==null)return;
            var visible=BrowserTabs.Items.Cast<TabItem>().Where(i=>i.Visibility==Visibility.Visible&&
                ((BrowserTab)i.Tag).IsPinned==tab.IsPinned).ToList();
            int target=visible.IndexOf(tab.Item)+direction;
            if(target<0||target>=visible.Count)return;
            MoveTabTo(tab,BrowserTabs.Items.IndexOf(visible[target]),direction>0);
        }
    }
}

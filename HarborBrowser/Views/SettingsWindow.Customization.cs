using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HarborBrowser.Models;
using HarborBrowser.Services;
using Microsoft.Win32;

namespace HarborBrowser.Views
{
    public partial class SettingsWindow
    {
        private List<Bookmark> _homeBookmarks;
        public string SelectedBackgroundPath {get;private set;}
        private void InitializeCustomization(IEnumerable<Bookmark> bookmarks)
        {
            _homeBookmarks=(bookmarks??Enumerable.Empty<Bookmark>()).Where(b=>b!=null).Select(b=>b.Copy()).Take(12).ToList();
            OnlineSuggestionsBox.IsChecked=_appearanceDraft.OnlineSuggestionsEnabled;
            HomeSceneBox.SelectedValue=_appearanceDraft.HomeScene??"Auto";
            if(HomeSceneBox.SelectedItem==null)HomeSceneBox.SelectedValue="Auto";
            UseBookmarkShortcutsBox.IsChecked=!_appearanceDraft.UseCustomHomeShortcuts;
            UpdateHomeSummary();RefreshPresets();
        }
        private void RefreshPresets()
        {
            PresetBox.ItemsSource=null;PresetBox.ItemsSource=_appearanceDraft.AppearancePresets??new List<AppearancePreset>();
        }
        private void SavePreset_Click(object sender,RoutedEventArgs e)
        {
            if(!CommitHex())return;
            string name=(PresetNameBox.Text??string.Empty).Trim();
            if(name.Length<1||name.Length>60||name.Any(char.IsControl)){MessageBox.Show(this,"Give the preset a name of 1–60 characters.","Appearance preset");return;}
            try{SavePreset(name);PresetBox.SelectedItem=_appearanceDraft.AppearancePresets.First(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));}catch(ArgumentException ex){MessageBox.Show(this,ex.Message,"Appearance preset");}
        }
        internal void SavePreset(string name)
        {
            if(_appearanceDraft.AppearancePresets==null)_appearanceDraft.AppearancePresets=new List<AppearancePreset>();
            var existing=_appearanceDraft.AppearancePresets.FirstOrDefault(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(existing==null&&_appearanceDraft.AppearancePresets.Count>=20)throw new ArgumentException("Keep up to 20 presets. Remove one before adding another.");
            if(existing!=null)_appearanceDraft.AppearancePresets.Remove(existing);
            _appearanceDraft.AppearancePresets.Add(new AppearancePreset{Name=name,ThemeMode=_appearanceDraft.ThemeMode,LightColors=_appearanceDraft.LightColors.Copy(),DarkColors=_appearanceDraft.DarkColors.Copy()});RefreshPresets();
        }
        private void ApplyPreset_Click(object sender,RoutedEventArgs e)
        {
            var preset=PresetBox.SelectedItem as AppearancePreset;if(preset==null)return;
            _appearanceDraft.ThemeMode=preset.ThemeMode;_appearanceDraft.LightColors=preset.LightColors.Copy();_appearanceDraft.DarkColors=preset.DarkColors.Copy();
            _updatingAppearance=true;AppearanceModeBox.SelectedValue=_appearanceDraft.ThemeMode;_updatingAppearance=false;RefreshAppearance();
        }
        private void RemovePreset_Click(object sender,RoutedEventArgs e){var preset=PresetBox.SelectedItem as AppearancePreset;if(preset==null)return;_appearanceDraft.AppearancePresets.Remove(preset);RefreshPresets();}
        private void BrowseBackground_Click(object sender,RoutedEventArgs e)
        {
            var picker=new OpenFileDialog{Filter="Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(picker.ShowDialog(this)!=true)return;
            try
            {
                var info=new System.IO.FileInfo(picker.FileName);if(info.Length>12*1024*1024)throw new ArgumentException("Choose an image smaller than 12 MB.");
                using(var stream=System.IO.File.OpenRead(picker.FileName)){var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,System.Windows.Media.Imaging.BitmapCacheOption.None);var frame=decoder.Frames[0];if((long)frame.PixelWidth*frame.PixelHeight>24000000)throw new ArgumentException("Choose an image with fewer than 24 million pixels.");}
                SelectedBackgroundPath=picker.FileName;HomeSceneBox.SelectedValue="Custom";UpdateHomeSummary();
            }
            catch(Exception ex){MessageBox.Show(this,"Could not use that image. "+ex.Message,"Home background");}
        }
        private void ResetBackground_Click(object sender,RoutedEventArgs e){SelectedBackgroundPath=null;_appearanceDraft.HomeBackgroundFile=null;HomeSceneBox.SelectedValue="Auto";UpdateHomeSummary();}
        private void EditShortcuts_Click(object sender,RoutedEventArgs e)
        {
            var initial=_appearanceDraft.UseCustomHomeShortcuts?_appearanceDraft.HomeShortcuts:_homeBookmarks;
            var dialog=new BookmarksWindow(initial,new string[0],true){Owner=this};
            if(dialog.ShowDialog()==true){_appearanceDraft.HomeShortcuts=dialog.ResultBookmarks;_appearanceDraft.UseCustomHomeShortcuts=true;UseBookmarkShortcutsBox.IsChecked=false;UpdateHomeSummary();}
        }
        private void ShortcutMode_Changed(object sender,RoutedEventArgs e)
        {
            if(_appearanceDraft==null)return;
            if(UseBookmarkShortcutsBox.IsChecked==false&&!_appearanceDraft.UseCustomHomeShortcuts)_appearanceDraft.HomeShortcuts=_homeBookmarks==null?new List<Bookmark>():_homeBookmarks.Select(b=>b.Copy()).ToList();
            _appearanceDraft.UseCustomHomeShortcuts=UseBookmarkShortcutsBox.IsChecked==false;UpdateHomeSummary();
        }
        private void UpdateHomeSummary()
        {
            BackgroundSummary.Text=SelectedBackgroundPath!=null?System.IO.Path.GetFileName(SelectedBackgroundPath):string.IsNullOrEmpty(_appearanceDraft.HomeBackgroundFile)?"No custom image selected":"A custom image is saved";
            ShortcutSummary.Text=_appearanceDraft.UseCustomHomeShortcuts?(_appearanceDraft.HomeShortcuts??new List<Bookmark>()).Count+" custom shortcuts":"Shows the first 12 bookmarks";
        }
    }
}

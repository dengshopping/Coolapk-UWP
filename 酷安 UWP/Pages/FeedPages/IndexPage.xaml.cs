﻿using CoolapkUWP.Control.ViewModels;
using CoolapkUWP.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace CoolapkUWP.Pages.FeedPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class IndexPage : Page, INotifyPropertyChanged
    {
        int page = 0;
        List<int> pages = new List<int>();
        string pageUrl;
        ObservableCollection<Entity> Collection = new ObservableCollection<Entity>();
        int index;
        List<string> urls = new List<string>();
        ObservableCollection<ObservableCollection<Entity>> Feeds2 = new ObservableCollection<ObservableCollection<Entity>>();
        InitialPage initialPage = null;

        public event PropertyChangedEventHandler PropertyChanged;

        private Thickness stackPanelMargin = new Thickness(0, Settings.FirstPageTitleHeight, 0, 2);
        public Thickness StackPanelMargin
        {
            get
            {
                if (initialPage != null) return stackPanelMargin;
                else return Settings.stackPanelMargin;
            }
            set
            {
                stackPanelMargin = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StackPanelMargin"));
            }
        }

        public IndexPage()
        {
            this.InitializeComponent();
            listView.ItemsSource = Collection;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            object[] vs = e.Parameter as object[];
            initialPage = vs[2] as InitialPage;
            if ((bool)vs[1]) TitleBar.Visibility = Visibility.Collapsed;
            pageUrl = vs[0] as string;
            TitleBar.BackButtonVisibility = Visibility.Visible;
            if (pageUrl.Contains("&title=")) TitleBar.Title = pageUrl.Substring(pageUrl.LastIndexOf("&title=") + 7);
            if (pageUrl.IndexOf("/page") == -1 && pageUrl != "/main/indexV8") pageUrl = "/page/dataList?url=" + pageUrl;
            else if (pageUrl.IndexOf("/page") == 0 && !pageUrl.Contains("/page/dataList")) pageUrl = pageUrl.Replace("/page", "/page/dataList");
            pageUrl = pageUrl.Replace("#", "%23");
            index = -1;
            GetUrlPage();
        }

        async void GetUrlPage(int p = -1)
        {
            if (index == -1)
            {
                if (!await GetUrlPage(p == -1 ? ++page : p, pageUrl, Collection))
                    page--;
            }
            else if (p == -1)
            {
                if (!await GetUrlPage(page = p == -1 ? ++pages[index] : p, urls[index], Feeds2[index]))
                    pages[index]--;
            }
        }

        async Task<bool> GetUrlPage(int page, string url, ObservableCollection<Entity> FeedsCollection)
        {
            Tools.ShowProgressBar();
            string s = await Tools.GetJson($"{url}{(url == "/main/indexV8" ? "?" : "&")}page={page}");
            JsonArray Root = Tools.GetDataArray(s);
            if (Root != null && Root.Count > 0)
                if (page == 1)
                {
                    int n = 0;
                    if (FeedsCollection.Count > 0)
                    {
                        var needDeleteItems = (from b in FeedsCollection
                                               from c in Root
                                               where b.entityId == c.GetObject()["entityId"].ToString().Replace("\"", string.Empty)
                                               select b).ToArray();
                        foreach (var item in needDeleteItems)
                            Collection.Remove(item);
                        n = (from b in FeedsCollection
                             where b.entityFixed
                             select b).Count();
                    }
                    int k = 0;
                    for (int i = 0; i < Root.Count; i++)
                    {
                        JsonObject jo = Root[i].GetObject();
                        if (index == -1 && jo.TryGetValue("entityTemplate", out IJsonValue t) && t?.GetString() == "configCard")
                        {
                            JsonObject j = JsonObject.Parse(jo["extraData"].GetString());
                            TitleBar.Title = j["pageTitle"].GetString();
                            continue;
                        }
                        if (jo.TryGetValue("entityTemplate", out IJsonValue tt) && tt.GetString() == "fabCard") continue;
                        FeedsCollection.Insert(n + k, GetEntity(jo));
                        k++;
                    }
                    Tools.HideProgressBar();
                    return true;
                }
                else
                {
                    if (Root.Count != 0)
                    {
                        foreach (var i in Root) FeedsCollection.Add(GetEntity(i.GetObject()));
                        Tools.HideProgressBar();
                        return true;
                    }
                    else
                    {
                        Tools.HideProgressBar();
                        return false;
                    }
                }
            return false;
        }

        Entity GetEntity(JsonObject token)
        {
            switch (token["entityType"].GetString())
            {
                case "feed": return new FeedViewModel(token, pageUrl == "/main/indexV8" ? FeedDisplayMode.isFirstPageFeed : FeedDisplayMode.normal);
                case "user": return new UserViewModel(token);
                case "topic": return new TopicViewModel(token);
                case "dyh": return new DyhViewModel(token);
                case "card":
                default: return new IndexPageViewModel(token);
            }
        }

        private void FeedListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Tools.OpenLink((sender as FrameworkElement).Tag as string);
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
            {
                if (Collection.Count != 0)
                    if (VScrollViewer.VerticalOffset == 0)
                    {
                        GetUrlPage(1);
                        VScrollViewer.ChangeView(null, 20, null);
                        refreshText.Visibility = Visibility.Collapsed;
                    }
                    else if (VScrollViewer.VerticalOffset == VScrollViewer.ScrollableHeight)
                        //if (string.IsNullOrEmpty(pageUrl)) GetIndexPage(++page);
                        GetUrlPage();
            }
            else refreshText.Visibility = Visibility.Visible;
        }

        public void RefreshPage()
        {
            GetUrlPage(1);
            VScrollViewer.ChangeView(null, 0, null);
        }

        private void Button_Click(object sender, RoutedEventArgs e) => Tools.Navigate(typeof(UserPage), (sender as FrameworkElement).Tag as string);
        private void TitleBar_BackButtonClick(object sender, RoutedEventArgs e) => Frame.GoBack();

        private void ListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            GetUrlPage(1);
            VScrollViewer.ChangeView(null, 0, null);
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element.Tag is string s) Tools.OpenLink(s);
            else if (element.Tag is IndexPageViewModel m)
            {
                if (string.IsNullOrEmpty(m.url)) return;
                string str = m.url;
                if (str.IndexOf("/page") == 0)
                {
                    str = str.Replace("/page", "/page/dataList");
                    str += $"&title={m.title}";
                    Tools.Navigate(typeof(IndexPage), new object[] { str, false, null });
                }
                else if (str.IndexOf('#') == 0) Tools.Navigate(typeof(IndexPage), new object[] { $"{str}&title={m.title}", false, null });
                else Tools.OpenLink(str);
            }
        }

        private void ListViewItem_Tapped_1(object sender, TappedRoutedEventArgs e)
        {
            IndexPageViewModel model = (sender as ListViewItem).DataContext as IndexPageViewModel;
            if (Feeds2.Count > 0)
            {
                ObservableCollection<Entity> feeds = Feeds2[0];
                var needDeleteItems = (from b in feeds
                                       where b.entityType == "feed"
                                       select b).ToArray();
                foreach (var item in needDeleteItems)
                    feeds.Remove(item);
                urls[0] = $"/page/dataList?url={model.url}&title={model.title}";
                urls[0] = urls[0].Replace("#", "%23");
                pages[0] = 0;

            }
            else
            {
                ObservableCollection<Entity> feeds = Collection;
                var needDeleteItems = (from b in feeds
                                       where b.entityType == "topic"
                                       select b).ToArray();
                foreach (var item in needDeleteItems)
                    feeds.Remove(item);
                pageUrl = $"/page/dataList?url={model.url}&title={model.title}";
                pageUrl = pageUrl.Replace("#", "%23");
                page = 0;
            }
            GetUrlPage();
        }

        public void ChangeTabView(string u)
        {
            pageUrl = u;
            page = 0;
            Collection.Clear();
            GetUrlPage();
        }

        private void VScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (initialPage != null)
            {
                double height = (sender as ScrollViewer).VerticalOffset - e.FinalView.VerticalOffset;
                Tools.mainPage.ChangeRowHeight(height);
                StackPanelMargin = new Thickness(0, initialPage.ChangeRowHeight(height), 0, 2);
            }
        }

        private void MarkdownTextBlock_ImageResolving(object sender, Microsoft.Toolkit.Uwp.UI.Controls.ImageResolvingEventArgs e) => Tools.SetEmojiPadding(sender);

        private void Pivot_Loaded(object sender, RoutedEventArgs e)
        {
            Pivot element = sender as Pivot;
            index = element.SelectedIndex;
            if (element.Items.Count == 0)
            {
                Entity[] f = element.Tag as Entity[];
                Style style = new Style(typeof(ListViewItem));
                style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
                for (int j = 0; j < f.Length; j++)
                {
                    IndexPageViewModel model = f[j] as IndexPageViewModel;
                    var ff = new ObservableCollection<Entity>();
                    var i = new PivotItem
                    {
                        Tag = f[j],
                        Content = new ListView
                        {
                            Style = Application.Current.Resources["ListViewStyle"] as Style,
                            ItemContainerStyle = style,
                            ItemTemplateSelector = Resources["FTemplateSelector"] as DataTemplateSelector,
                            ItemsSource = ff
                        },
                        Header = model.title
                    };
                    element.Items.Add(i);
                    pages.Add(1);
                    Feeds2.Add(ff);
                    urls.Add("/page/dataList?url=" + model.url.Replace("#", "%23") + $"&title={model.title}");
                    if (j == 0) load(element, i);
                }
                return;
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e) => load(sender as Pivot);

        void load(Pivot element, PivotItem i = null)
        {
            PivotItem item = i is null ? element.SelectedItem as PivotItem : i;
            IndexPageViewModel model = item.Tag as IndexPageViewModel;
            ListView view = item.Content as ListView;
            ObservableCollection<Entity> feeds = view.ItemsSource as ObservableCollection<Entity>;
            string u = model.url;
            u = u.Replace("#", "%23");
            u = "/page/dataList?url=" + u + $"&title={model.title}";
            _ = GetUrlPage(1, u, feeds);
        }
    }
}
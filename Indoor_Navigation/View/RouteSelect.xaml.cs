using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Indoor_Navigation.Algorithm;
using Indoor_Navigation.Model;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using 路径导航.ViewModel;


namespace Indoor_Navigation.View
{
    public sealed partial class RouteSelect : Page
    {
        #region 标志域字段
        ObservableCollection<DBItem> display = new ObservableCollection<DBItem>();//显示已选择路径的数据绑定对象
        List<int> selected = new List<int>();//已选择目的地点集合
        //目的地绑定数据源
        List<DBItem> L21 = DataBase.Point.GetRange(19, 48);
        List<DBItem> L22 = DataBase.Point.GetRange(105, 37);
        List<DBItem> L11 = DataBase.Point.GetRange(175, 64);
        List<DBItem> L12 = DataBase.Point.GetRange(263, 29);
        List<DBItem> L1 = DataBase.Point.GetRange(296, 3);
        
        #endregion
        #region 交互函数
        public RouteSelect()
        {
            this.InitializeComponent();
            F21.Width = Convert.ToDouble(CookieHelper.Read("Width")) - 166;
            F22.Width = Convert.ToDouble(CookieHelper.Read("Width")) - 166;
            F11.Width = Convert.ToDouble(CookieHelper.Read("Width")) - 166;
            F12.Width = Convert.ToDouble(CookieHelper.Read("Width")) - 166;
            B1.Width = Convert.ToDouble(CookieHelper.Read("Width")) - 166;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            L21.RemoveRange(22, 2);
            L22.RemoveRange(29, 6);
            L11.RemoveRange(32, 4);
            L12.RemoveRange(20, 4);
            F21.ItemsSource =L21;
            F22.ItemsSource = L22;
            F11.ItemsSource = L11;
            F12.ItemsSource = L12;
            B1.ItemsSource = L1;
            selectedPoint.ItemsSource = display;
        }

        private void Return_Click(object sender, RoutedEventArgs e)
        {
            if (selected.Count > 0)
                Frame.Navigate(typeof(MainPage), selected);
            else
                Frame.Navigate(typeof(MainPage), new List<int>(){-1});
        }

        private void F21Add_Click(object sender, RoutedEventArgs e)
        {
            DBItem selectedItem = F21.SelectedItem as DBItem;
            if (selectedItem != null)
            {
                selected.Add(selectedItem.Id);
                display.Add(new DBItem() { Id = selectedItem.Id, Name = "二楼登机区 " + selectedItem.Name, X = selectedItem.X, Y = selectedItem.Y });
            }
        }

        private void F22Add_Click(object sender, RoutedEventArgs e)
        {
            DBItem selectedItem = F22.SelectedItem as DBItem;
            if (selectedItem != null)
            {
                selected.Add(selectedItem.Id);
                display.Add(new DBItem() { Id = selectedItem.Id, Name = "二楼公共区 " + selectedItem.Name, X = selectedItem.X, Y = selectedItem.Y });
            }
        }

        private void F11Add_Click(object sender, RoutedEventArgs e)
        {
            DBItem selectedItem = F11.SelectedItem as DBItem;
            if (selectedItem != null)
            {
                selected.Add(selectedItem.Id);
                display.Add(new DBItem() { Id = selectedItem.Id, Name = "一楼下机区 " + selectedItem.Name, X = selectedItem.X, Y = selectedItem.Y });
            }
        }

        private void F12Add_Click(object sender, RoutedEventArgs e)
        {
            DBItem selectedItem = F12.SelectedItem as DBItem;
            if (selectedItem != null)
            {
                selected.Add(selectedItem.Id);
                display.Add(new DBItem() { Id = selectedItem.Id, Name = "一楼公共区 " + selectedItem.Name, X = selectedItem.X, Y = selectedItem.Y });
            }
        }

        private void B1Add_Click(object sender, RoutedEventArgs e)
        {
            DBItem selectedItem = B1.SelectedItem as DBItem;
            if (selectedItem != null)
            {
                selected.Add(selectedItem.Id);
                display.Add(new DBItem() { Id = selectedItem.Id, Name = "地下停车场 " + selectedItem.Name, X = selectedItem.X, Y = selectedItem.Y });
            }
        }

        private void Deleted_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            DBItem t = item.DataContext as DBItem;
            display.Remove(t);
            selected.Remove(t.Id);
        }

        private void SelectListItem_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class UriInput : Page
    {
        public UriInput()
        {
            this.InitializeComponent();
            Rect displayScreen = Windows.UI.Core.CoreWindow.GetForCurrentThread().Bounds;
            CookieHelper.Write("Width", displayScreen.Width.ToString());
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CookieHelper.Write("Uri", InputUri.Text);
            Frame.Navigate(typeof(MainPage));
        }
    }
}

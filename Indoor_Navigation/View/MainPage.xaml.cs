using System;
using System.Collections.Generic;
using System.Linq;
using Indoor_Navigation.Algorithm;
using Indoor_Navigation.View;
using Windows.Devices.Sensors;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Phone.Devices.Notification;
using Windows.Phone.UI.Input;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using 路径导航.Model;
using 路径导航.ViewModel;

namespace Indoor_Navigation
{
    public sealed partial class MainPage : Page
    {
        #region 标志域字段
        DispatcherTimer mainTimer = new DispatcherTimer();//时间间隔触发器
        int toastset = -1;//双击退出程序标志域
        XYPos curPos = new XYPos() { X = 0.0, Y = 0.0, Floor = -1 };//当前所在位置
        XYPos lastPoint;//当前点段起点
        XYPos nextPoint = null;//当前点段终点
        List<int> route = new List<int>();//规划路径
        List<int> keyPoint = new List<int>();//中转点及目的地
        Compass compass = Compass.GetDefault();//电子罗盘
        double mapWidth;//地图宽度
        int nextPointNum = 1;//当前路径上点指针
        string uri;//存储服务器地址
        bool IsCompassOn = false;//手机是否支持陀螺仪标志
        VibrationDevice testVibrationDevice = VibrationDevice.GetDefault();
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        SpeechSynthesisStream synthesisStream;
        #endregion
        #region 交互函数
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            uri = CookieHelper.Read("Uri");
            if (compass != null)
            {
                IsCompassOn = true;
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ScreenSet();
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            HardwareButtons.CameraPressed += HardwareButtons_CameraPressed;
            mainTimer.Interval = new TimeSpan(0, 0, 1);
            mainTimer.Tick += MainTimer_Tick;
            nextPointNum = 1;
            if (IsCompassOn)
            {
                compass.ReadingChanged += compass_ReadingChanged;
            }
            keyPoint = e.Parameter as List<int>;
            SetCurPosition();
            curPoint.Begin();
            if (keyPoint != null)
            {
                if(keyPoint.ElementAt(0) != -1)
                {
                    DrawLines();
                    PointSectionInit();
                    Toast("导航开始，请按照路径行走");
                }
                else
                {
                    Toast("未选择目的地");
                }
            }
            mainTimer.Start();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mainTimer.Tick -= MainTimer_Tick;
            if (IsCompassOn)
            {
                compass.ReadingChanged -= compass_ReadingChanged;
            }
            mainTimer.Stop();
            curPoint.Stop();
            DeleteAllLine();
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            HardwareButtons.CameraPressed -= HardwareButtons_CameraPressed;
        }
        void HardwareButtons_CameraPressed(object sender, CameraEventArgs e)
        {
            Cortana();
        }
        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            pop.IsOpen = true;
            DispatcherTimer dct = new DispatcherTimer();
            dct.Interval = new TimeSpan(0, 0, 2);
            dct.Tick += dct_Tick;
            dct.Start();
            toastset *= -1;
            st1.Begin();
            if (toastset == -1)
            {
                Application.Current.Exit();
            }
            e.Handled = true;
        }
        async void compass_ReadingChanged(Compass sender, CompassReadingChangedEventArgs args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                CompassReading reading = args.Reading;
                F2Map.RenderTransform.SetValue(RotateTransform.AngleProperty, 360 - reading.HeadingTrueNorth.Value);
                F1Map.RenderTransform.SetValue(RotateTransform.AngleProperty, 360 - reading.HeadingTrueNorth.Value);
                B1Map.RenderTransform.SetValue(RotateTransform.AngleProperty, 360 - reading.HeadingTrueNorth.Value);
                icon.RenderTransform.SetValue(RotateTransform.AngleProperty, 300 - reading.HeadingTrueNorth.Value);
            });
        }
        void dct_Tick(object sender, object e)
        {
            toastset *= -1;
        }
        private void RouteSelect_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(RouteSelect));
        }
        void MainTimer_Tick(object sender, object e)
        {
            SetCurPosition();
            if (nextPoint != null)
            {
                ReachDestination();
                OffsetCheck();
            }
            PointContinueNext();
        }
        private void Locate_Click(object sender, RoutedEventArgs e)
        {
            SetCurPosition();
            B1DisPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
            B1DisPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
            F1DisPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
            F1DisPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
            F2DisPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
            F2DisPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
            DisPoint.Begin();
        }
        private void Toilet_Click(object sender, RoutedEventArgs e)
        {
            Toilet_f();
        }
        private void Entry_Click(object sender, RoutedEventArgs e)
        {
            Entry_f();
        }
        private void Store_Click(object sender, RoutedEventArgs e)
        {
            Store_f();
        }
        private void Question_Click(object sender, RoutedEventArgs e)
        {
            Question_f();
        }
        private void Drink_Click(object sender, RoutedEventArgs e)
        {
            Drink_f();
        }
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopNavigate();
            Toast("导航终止");
        }
        #endregion
        #region 逻辑方法
        private void DrawLines()//将路径画在地图上
        {
            keyPoint.Insert(0, Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            route = Tools.Calculate(keyPoint);
            //路径前三点最优化
            double x1 = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X;
            double y1 = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y;
            double x2 = DataBase.Point.Where((item) => item.Id == route.ElementAt(1)).First().X;
            double y2 = DataBase.Point.Where((item) => item.Id == route.ElementAt(1)).First().Y;
            if ((Math.Sqrt((curPos.X - x1) * (curPos.X - x1) + (curPos.Y - y1) * (curPos.Y - y1))
                + Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)))
                > Math.Sqrt((curPos.X - x2) * (curPos.X - x2) + (curPos.Y - y2) * (curPos.Y - y2)))
                route.RemoveAt(0);
            DrawFirstLine();
            for (int i = 0; i < route.Count - 1; i++)
            {
                if ((route.ElementAt(i) <= 141 && route.ElementAt(i + 1) <= 141))
                {
                    Line line2 = new Line();
                    line2.X1 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().X;
                    line2.Y1 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().Y;
                    line2.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().X;
                    line2.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().Y;
                    line2.Stroke = new SolidColorBrush(Colors.Black);
                    line2.StrokeThickness = 3;
                    line2.StrokeLineJoin = PenLineJoin.Round;
                    line2.StrokeStartLineCap = PenLineCap.Round;
                    line2.StrokeEndLineCap = PenLineCap.Round;
                    F2Map.Children.Add(line2);
                }
                else if((route.ElementAt(i) > 141 && route.ElementAt(i) <= 291 && route.ElementAt(i + 1) > 141 && route.ElementAt(i + 1) <= 291))
                {
                    Line line1 = new Line();
                    line1.X1 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().X;
                    line1.Y1 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().Y;
                    line1.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().X;
                    line1.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().Y;
                    line1.Stroke = new SolidColorBrush(Colors.Black);
                    line1.StrokeThickness = 3;
                    line1.StrokeLineJoin = PenLineJoin.Round;
                    line1.StrokeStartLineCap = PenLineCap.Round;
                    line1.StrokeEndLineCap = PenLineCap.Round;
                    F1Map.Children.Add(line1);
                }
                else if((route.ElementAt(i) > 291 && route.ElementAt(i + 1) > 291))
                {
                    Line line0 = new Line();
                    line0.X1 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().X;
                    line0.Y1 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i)).First().Y;
                    line0.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().X;
                    line0.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(i + 1)).First().Y;
                    line0.Stroke = new SolidColorBrush(Colors.Black);
                    line0.StrokeThickness = 3;
                    line0.StrokeLineJoin = PenLineJoin.Round;
                    line0.StrokeStartLineCap = PenLineCap.Round;
                    line0.StrokeEndLineCap = PenLineCap.Round;
                    B1Map.Children.Add(line0);
                }
            }
        }
        private void DeleteLastLine()//清除最近一条线
        {
            switch (curPos.Floor)
            {
                case 2:
                    DependencyObject t2 = VisualTreeHelper.GetChild(F2Map, 2);
                    Line a2 = t2 as Line;
                    F2Map.Children.Remove(a2);
                    break;
                case 1:
                    DependencyObject t1 = VisualTreeHelper.GetChild(F1Map, 2);
                    Line a1 = t1 as Line;
                    F1Map.Children.Remove(a1);
                    break;
                case 0:
                    DependencyObject t0 = VisualTreeHelper.GetChild(B1Map, 2);
                    Line a0 = t0 as Line;
                    B1Map.Children.Remove(a0);
                    break;

            }
        }
        private void DeleteAllLine()//删除所有线
        {
            int n2 = VisualTreeHelper.GetChildrenCount(F2Map);
            int n1 = VisualTreeHelper.GetChildrenCount(F1Map);
            int n0 = VisualTreeHelper.GetChildrenCount(B1Map);
            for (int i = 2; i < n2; i++)
            {
                DependencyObject t2 = VisualTreeHelper.GetChild(F2Map, 2);
                Line a2 = t2 as Line;
                F2Map.Children.Remove(a2);
            }
            for (int i = 2; i < n1; i++)
            {
                DependencyObject t1 = VisualTreeHelper.GetChild(F1Map, 2);
                Line a1 = t1 as Line;
                F1Map.Children.Remove(a1);
            }
            for (int i = 2; i < n0; i++)
            {
                DependencyObject t0 = VisualTreeHelper.GetChild(B1Map, 2);
                Line a0 = t0 as Line;
                B1Map.Children.Remove(a0);
            }

        }
        private void DrawFirstLine()//画出当前位置到第一个关键点路径
        {
            Line line = new Line();
            line.X1 = mapWidth * curPos.X;
            line.Y1 = mapWidth - mapWidth * curPos.Y;
            if (route.ElementAt(0) <= 141)
            {
                line.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X;
                line.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y;
                line.Stroke = new SolidColorBrush(Colors.Black);
                line.StrokeThickness = 3;
                line.StrokeLineJoin = PenLineJoin.Round;
                line.StrokeStartLineCap = PenLineCap.Round;
                line.StrokeEndLineCap = PenLineCap.Round;
                F2Map.Children.Add(line);
            }
            else if(route.ElementAt(0) <= 291)
            {
                line.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X;
                line.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y;
                line.Stroke = new SolidColorBrush(Colors.Black);
                line.StrokeThickness = 3;
                line.StrokeLineJoin = PenLineJoin.Round;
                line.StrokeStartLineCap = PenLineCap.Round;
                line.StrokeEndLineCap = PenLineCap.Round;
                F1Map.Children.Add(line);
            }
            else
            {
                line.X2 = mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X;
                line.Y2 = mapWidth - mapWidth * DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y;
                line.Stroke = new SolidColorBrush(Colors.Black);
                line.StrokeThickness = 3;
                line.StrokeLineJoin = PenLineJoin.Round;
                line.StrokeStartLineCap = PenLineCap.Round;
                line.StrokeEndLineCap = PenLineCap.Round;
                B1Map.Children.Add(line);
            }

        }
        private void PointContinueNext()//检查当前点段是否走完并当前点段继续后移
        {
            if (nextPoint != null)
            {
                if (Tools.IsReachPoint(nextPoint, curPos))
                {
                    if (nextPointNum == route.Count)
                    {
                        nextPoint = null;
                        DeleteLastLine();
                        nextPointNum = 1;
                        route = new List<int>();
                        return;
                    }
                    if (lastPoint.Floor == nextPoint.Floor)
                    {
                        DeleteLastLine();
                    }
                    lastPoint = nextPoint;
                    if (route.ElementAt(nextPointNum) <= 141)
                    {
                        nextPoint = new XYPos()
                        {
                            Floor = 2,
                            X = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().X,
                            Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().Y
                        };
                    }
                    else if (route.ElementAt(nextPointNum) <= 291)
                    {
                        nextPoint = new XYPos()
                            {
                                Floor = 1,
                                X = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().X,
                                Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().Y
                            };
                    }
                    else
                    {
                        nextPoint = new XYPos()
                            {
                                Floor = 0,
                                X = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().X,
                                Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(nextPointNum)).First().Y
                            };
                    }
                    nextPointNum++;
                }
            }

        }
        private async void SetCurPosition()//设置当前坐标的相关内容
        {
            XYPos getPos = new XYPos();
            try
            {
                getPos = await Tools.GetCurPos(uri);//获取当前位置
            }
            catch (Exception)
            {
                new MessageDialog("无法从服务器获取位置信息,程序即将退出！").ShowAsync();
                Application.Current.Exit();

            }
            if (getPos.Floor != curPos.Floor)
            {
                switch (getPos.Floor)//设置当前楼层
                {
                    case 0:
                        F2Map.Visibility = Visibility.Collapsed;
                        F1Map.Visibility = Visibility.Collapsed;
                        B1Map.Visibility = Visibility.Visible;
                        DisplayCurrentState.Text = "地下一层";
                        break;
                    case 1:
                        F2Map.Visibility = Visibility.Collapsed;
                        F1Map.Visibility = Visibility.Visible;
                        B1Map.Visibility = Visibility.Collapsed;
                        DisplayCurrentState.Text = "地上一层";
                        break;
                    case 2:
                        F2Map.Visibility = Visibility.Visible;
                        F1Map.Visibility = Visibility.Collapsed;
                        B1Map.Visibility = Visibility.Collapsed;
                        DisplayCurrentState.Text = "地上二层";
                        break;
                }
            }
            curPos = getPos;
            switch (curPos.Floor)//设置当前楼层
            {
                case 0:
                    B1CurPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
                    B1CurPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
                    break;
                case 1:
                    F1CurPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
                    F1CurPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
                    break;
                case 2:
                    F2CurPoint.SetValue(Canvas.LeftProperty, curPos.X * mapWidth);
                    F2CurPoint.SetValue(Canvas.TopProperty, (1 - curPos.Y) * mapWidth);
                    break;
            }
        }
        private void PointSectionInit()//点段初始化(route[0][1]两点）
        {
            if (route.ElementAt(0) <= 141)
            {
                lastPoint = new XYPos()
                {
                    Floor = 2,
                    X = curPos.X,
                    Y = curPos.Y
                };
                nextPoint = new XYPos()
                {
                    Floor = 2,
                    X = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X,
                    Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y
                };
            }
            else if (route.ElementAt(0) <= 291)
            {
                lastPoint = new XYPos()
                {
                    Floor = 1,
                    X = curPos.X,
                    Y = curPos.Y
                };
                nextPoint = new XYPos()
                {
                    Floor = 1,
                    X = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X,
                    Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y
                };
            }
            else
            {
                lastPoint = new XYPos()
                {
                    Floor = 0,
                    X = curPos.X,
                    Y = curPos.Y
                };
                nextPoint = new XYPos()
                {
                    Floor = 0,
                    X = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().X,
                    Y = DataBase.Point.Where((item) => item.Id == route.ElementAt(0)).First().Y
                };
            }
            
        }
        private void ReachDestination()//判断到达目的地或中转点提醒用户
        {
            if (Tools.IsReachPoint(nextPoint, curPos))
            {
                if (route.ElementAt(nextPointNum - 1) == keyPoint.ElementAt(1))
                {
                    string displayName = "";
                    if(keyPoint.ElementAt(1) <= 141)
                    {
                        displayName = "二楼" + DataBase.Point.Where((i) => i.Id == keyPoint.ElementAt(1)).First().Name;
                    }
                    else if(keyPoint.ElementAt(1) <= 291)
                    {
                        displayName = "一楼" + DataBase.Point.Where((i) => i.Id == keyPoint.ElementAt(1)).First().Name;
                    }
                    else
                    {
                        displayName = "地下停车场" + DataBase.Point.Where((i) => i.Id == keyPoint.ElementAt(1)).First().Name;
                    }
                    keyPoint.Remove(keyPoint.ElementAt(1));
                    Toast("到达：" + displayName);
                    testVibrationDevice.Vibrate(TimeSpan.FromSeconds(0.5));
                }
                //上下楼检测
                else if ((route.ElementAt(nextPointNum - 1) == 41) && (route.ElementAt(nextPointNum) == 209) ||
                    (route.ElementAt(nextPointNum - 1) == 42) && (route.ElementAt(nextPointNum) == 210) ||
                    (route.ElementAt(nextPointNum - 1) == 138) && (route.ElementAt(nextPointNum) == 207) ||
                    (route.ElementAt(nextPointNum - 1) == 139) && (route.ElementAt(nextPointNum) == 208) ||
                    (route.ElementAt(nextPointNum - 1) == 136) && (route.ElementAt(nextPointNum) == 285) ||
                    (route.ElementAt(nextPointNum - 1) == 137) && (route.ElementAt(nextPointNum) == 286) ||
                    (route.ElementAt(nextPointNum - 1) == 285) && (route.ElementAt(nextPointNum) == 294) ||
                    (route.ElementAt(nextPointNum - 1) == 286) && (route.ElementAt(nextPointNum) == 295) ||
                    (route.ElementAt(nextPointNum - 1) == 134) && (route.ElementAt(nextPointNum) == 283) ||
                    (route.ElementAt(nextPointNum - 1) == 135) && (route.ElementAt(nextPointNum) == 284) ||
                    (route.ElementAt(nextPointNum - 1) == 283) && (route.ElementAt(nextPointNum) == 292) ||
                    (route.ElementAt(nextPointNum - 1) == 284) && (route.ElementAt(nextPointNum) == 293) )
                {
                    Toast("请下楼");
                }
                else if((route.ElementAt(nextPointNum - 1) == 209) && (route.ElementAt(nextPointNum) == 41) ||
                    (route.ElementAt(nextPointNum - 1) == 210) && (route.ElementAt(nextPointNum) == 42) ||
                    (route.ElementAt(nextPointNum - 1) == 207) && (route.ElementAt(nextPointNum) == 138) ||
                    (route.ElementAt(nextPointNum - 1) == 208) && (route.ElementAt(nextPointNum) == 139) ||
                    (route.ElementAt(nextPointNum - 1) == 285) && (route.ElementAt(nextPointNum) == 136) ||
                    (route.ElementAt(nextPointNum - 1) == 286) && (route.ElementAt(nextPointNum) == 137) ||
                    (route.ElementAt(nextPointNum - 1) == 294) && (route.ElementAt(nextPointNum) == 285) ||
                    (route.ElementAt(nextPointNum - 1) == 295) && (route.ElementAt(nextPointNum) == 286) ||
                    (route.ElementAt(nextPointNum - 1) == 283) && (route.ElementAt(nextPointNum) == 134) ||
                    (route.ElementAt(nextPointNum - 1) == 284) && (route.ElementAt(nextPointNum) == 135) ||
                    (route.ElementAt(nextPointNum - 1) == 292) && (route.ElementAt(nextPointNum) == 283) ||
                    (route.ElementAt(nextPointNum - 1) == 293) && (route.ElementAt(nextPointNum) == 284) )
                {
                    Toast("请上楼");
                }
            }
        }
        private void OffsetCheck()//路径是否走偏并重新规划路径
        {
            if (Tools.IsOffset(lastPoint, nextPoint, curPos))
            {
                mainTimer.Stop();
                Toast("偏离预定路径，路径重新规划！");
                testVibrationDevice.Vibrate(TimeSpan.FromSeconds(1));
                DeleteAllLine();
                keyPoint.RemoveAt(0);
                nextPointNum = 1;
                nextPoint = null;
                DrawLines();
                PointSectionInit();
                mainTimer.Start();
            }

        }
        private void StopNavigate()//终止导航
        {
            DeleteAllLine();
            nextPoint = null;
            nextPointNum = 1;
            keyPoint = new List<int>();
            route = new List<int>();

        }
        private void Toilet_f()//卫生间导航
        {
            pro.Visibility = Visibility.Visible;
            filter.Visibility = Visibility.Visible;
            mainTimer.Stop();
            double maxLength = double.MaxValue;
            double curLength;
            int endPoint = -1;//记录终点
            StopNavigate();
            keyPoint.Add(Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            if (keyPoint.ElementAt(0) <= 66)
            {
                List<int> candidate = new List<int>() { 37, 38, 39, 40 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength < maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 141)
            {
                List<int> candidate = new List<int>() { 130, 131, 132, 133 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength < maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 238)
            {
                List<int> candidate = new List<int>() { 193, 194, 195, 196 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else
            {
                List<int> candidate = new List<int>() { 279, 280, 281, 282 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            keyPoint.Clear();
            keyPoint.Add(endPoint);
            DrawLines();
            PointSectionInit();
            Toast("导航开始，请按照路径行走");
            mainTimer.Start();
            pro.Visibility = Visibility.Collapsed;
            filter.Visibility = Visibility.Collapsed;
        }
        private void Entry_f()//登机导航
        {
            pro.Visibility = Visibility.Visible;
            filter.Visibility = Visibility.Visible;
            mainTimer.Stop();
            double maxLength = double.MaxValue;
            double curLength;
            int endPoint = -1;//记录终点
            StopNavigate();
            keyPoint.Add(Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            List<int> candidate = new List<int>() { 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36 };
            foreach (int item in candidate)
            {
                keyPoint.Add(item);
                curLength = Tools.Distance(keyPoint);
                if (curLength <= maxLength)
                {
                    maxLength = curLength;
                    endPoint = item;
                }
                keyPoint.RemoveAt(1);
            }
            keyPoint.Clear();
            keyPoint.Add(endPoint);
            DrawLines();
            PointSectionInit();
            Toast("导航开始，请按照路径行走");
            mainTimer.Start();
            pro.Visibility = Visibility.Collapsed;
            filter.Visibility = Visibility.Collapsed;
        }
        private void Store_f()//商店导航
        {
            pro.Visibility = Visibility.Visible;
            filter.Visibility = Visibility.Visible;
            mainTimer.Stop();
            double maxLength = double.MaxValue;
            double curLength;
            int endPoint = -1;//记录终点
            StopNavigate();
            keyPoint.Add(Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            if (keyPoint.ElementAt(0) <= 66)
            {
                List<int> candidate = new List<int>() { 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 141)
            {
                List<int> candidate = new List<int>() { 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 238)
            {
                List<int> candidate = new List<int>() { 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else
            {
                List<int> candidate = new List<int>() { 263, 264, 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            keyPoint.Clear();
            keyPoint.Add(endPoint);
            DrawLines();
            PointSectionInit();
            Toast("导航开始，请按照路径行走");
            mainTimer.Start();
            pro.Visibility = Visibility.Collapsed;
            filter.Visibility = Visibility.Collapsed;
        }
        private void Question_f()//问询处导航
        {
            pro.Visibility = Visibility.Visible;
            filter.Visibility = Visibility.Visible;
            mainTimer.Stop();
            double maxLength = double.MaxValue;
            double curLength;
            int endPoint = -1;//记录终点
            StopNavigate();
            keyPoint.Add(Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            List<int> candidate = new List<int>() { 140, 141, 287, 288 };
            foreach (int item in candidate)
            {
                keyPoint.Add(item);
                curLength = Tools.Distance(keyPoint);
                if (curLength <= maxLength)
                {
                    maxLength = curLength;
                    endPoint = item;
                }
                keyPoint.RemoveAt(1);
            }
            keyPoint.Clear();
            keyPoint.Add(endPoint);
            DrawLines();
            PointSectionInit();
            Toast("导航开始，请按照路径行走");
            mainTimer.Start();
            pro.Visibility = Visibility.Collapsed;
            filter.Visibility = Visibility.Collapsed;
        }
        private void Drink_f()//饮水处导航
        {
            pro.Visibility = Visibility.Visible;
            filter.Visibility = Visibility.Visible;
            mainTimer.Stop();
            double maxLength = double.MaxValue;
            double curLength;
            int endPoint = -1;//记录终点
            StopNavigate();
            keyPoint.Add(Tools.GetLateKeyPoint(curPos.X, curPos.Y, curPos.Floor));
            if (keyPoint.ElementAt(0) <= 66)
            {
                List<int> candidate = new List<int>() { 61, 62, 63, 64, 65, 66 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 141)
            {
                List<int> candidate = new List<int>() { 130, 131, 132, 133 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else if (keyPoint.ElementAt(0) <= 238)
            {
                List<int> candidate = new List<int>() { 195, 196, 197, 198, 199, 200 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            else
            {
                List<int> candidate = new List<int>() { 279, 280, 281, 282 };
                foreach (int item in candidate)
                {
                    keyPoint.Add(item);
                    curLength = Tools.Distance(keyPoint);
                    if (curLength <= maxLength)
                    {
                        maxLength = curLength;
                        endPoint = item;
                    }
                    keyPoint.RemoveAt(1);
                }
            }
            keyPoint.Clear();
            keyPoint.Add(endPoint);
            DrawLines();
            PointSectionInit();
            Toast("导航开始，请按照路径行走");
            mainTimer.Start();
            pro.Visibility = Visibility.Collapsed;
            filter.Visibility = Visibility.Collapsed;
        }
        private void Toast(string text)//Toast通知
        {
            ReachToastText.Text = text;
            reachToast.IsOpen = true;
            DispatcherTimer dc = new DispatcherTimer();
            dc.Interval = new TimeSpan(0, 0, 5);
            dc.Start();
            st2.Begin();
            Speech(text);
            text = "";
        }
        private async void Cortana()//小娜语音识别
        {
            string message = "";
          
            try
            {
                SpeechRecognizer speechRecognizer = new SpeechRecognizer();
                SpeechRecognitionListConstraint list = new SpeechRecognitionListConstraint(new List<string>
                {
                    "卫生间",
                    "厕所",
                    "登机",
                    "商店",
                    "问询",
                    "饮水",
                    "喝水",
                    "购物",
                    "买东西"
                });
                list.Tag = "Destination";
                speechRecognizer.Constraints.Add(list);
                SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();
                speechRecognizer.UIOptions.ExampleText = "大声告诉我你要去哪";
                var result = await speechRecognizer.RecognizeWithUIAsync();
                if (result.Confidence == SpeechRecognitionConfidence.Rejected)
                {
                    message = "语音识别不到";
                }
                else
                {
                    switch(result.Text)
                    {
                        case "卫生间":
                        case "厕所":
                            Toilet_f();
                            break;
                        case "登机":
                            Entry_f();
                            break;
                        case "商店":
                        case "购物":
                        case "买东西":
                            Store_f();
                            break;
                        case "问询":
                            Question_f();
                            break;
                        case "饮水":
                        case "喝水":
                            Drink_f();
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                message = "语音识别异常，信息：" + err.Message + err.HResult;
            }
            if (message!="")
            await new MessageDialog(message).ShowAsync();
        }
        private async void Speech(string speakText)//播报语音
        {
            try
            {
                synthesisStream = await synthesizer.SynthesizeTextToStreamAsync(speakText);
            }
            catch (Exception)
            {
                synthesisStream = null;
            }
            if (synthesisStream == null)
            {
                //await new MessageDialog("播报语音发生错误！").ShowAsync();
                return;
            }
            else
            {
                this.media.AutoPlay = true;
                this.media.SetSource(synthesisStream, synthesisStream.ContentType);
                this.media.Play();
            }
        }
        private void ScreenSet()//根据分辨率自适应控件大小
        {
            mapWidth = Convert.ToDouble(CookieHelper.Read("Width"));
            F2Map.Width = mapWidth;
            F2Map.Height = mapWidth;
            F1Map.Width = mapWidth;
            F1Map.Height = mapWidth;
            B1Map.Width = mapWidth;
            B1Map.Height = mapWidth;
            popGrid.Width = mapWidth;
            reachToastGrid.Width = mapWidth;
            F2Map.RenderTransform.SetValue(RotateTransform.CenterXProperty, mapWidth / 2);
            F2Map.RenderTransform.SetValue(RotateTransform.CenterYProperty, mapWidth / 2);
            F1Map.RenderTransform.SetValue(RotateTransform.CenterXProperty, mapWidth / 2);
            F1Map.RenderTransform.SetValue(RotateTransform.CenterYProperty, mapWidth / 2);
            B1Map.RenderTransform.SetValue(RotateTransform.CenterXProperty, mapWidth / 2);
            B1Map.RenderTransform.SetValue(RotateTransform.CenterYProperty, mapWidth / 2);
        }
        #endregion
    }
}

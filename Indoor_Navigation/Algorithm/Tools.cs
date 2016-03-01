using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Indoor_Navigation.Model;
using Windows.Data.Json;
using 路径导航.Model;

namespace Indoor_Navigation.Algorithm
{
    public static class Tools
    {
        public static List<int> Calculate(List<int> route)//计算最短路径
        {
            if (route.Count() <= 1)
            {
                return route;
            }
            List<int> result = new List<int>();
            for (int i = 0; i < route.Count() - 1; i++)
            {
                Beeline line = new Beeline();
                line.SetSP(route.ElementAt(i));
                line.Dijkstra();
                result.AddRange((line.OutPoint(route.ElementAt(i + 1))));
            }
            for (int i = 0; i < result.Count - 1; i++)
            {
                if (result.ElementAt(i) == result.ElementAt(i + 1))
                    result.RemoveAt(i);
            }
            return result;
        }
        public static double Distance(List<int> route)//计算两点间最短距离
        {
            Beeline line = new Beeline();
            line.SetSP(route.ElementAt(0));
            line.Dijkstra();
            return line.D[route.ElementAt(1)];
        }
        public static int GetDivision(double x, double y, int floor)//根据xy坐标获取当前坐标所在区域
        {
            if (floor == 1)//F1层
            {
                if (y >= 0.38 ||
                        x * x + (y - 0.34) * (y - 0.34) <= 0.04 ||
                        (x - 1) * (x - 1) + (y - 0.34) * (y - 0.34) <= 0.04 ||
                        (x <= 0.2 && y >= 0.34) ||
                        (x >= 0.8 && y >= 0.34))
                    return 11;
                else
                    return 12;
            }
            else if (floor == 2)//F2层
            {
                if (y >= 0.8 ||
                        x * x + (y - 0.34) * (y - 0.34) <= 0.04 ||
                        (x - 1) * (x - 1) + (y - 0.34) * (y - 0.34) <= 0.04 ||
                        (x <= 0.2 && y >= 0.34) ||
                        (x >= 0.8 && y >= 0.34))
                    return 21;
                else
                    return 22;
            }
            else if (floor == 0)//地下一层
            {
                return 0;
            }
            else//错误输入，则返回错误代码-1
                return -1;
        }
        public async static Task<XYPos> GetCurPos(string Uri)//通过服务器获得当前坐标信息
        {
            WebRequest request = HttpWebRequest.Create(Uri);
            request.Method = "GET";
            request.Headers[HttpRequestHeader.Pragma] = "no-cache";
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            try
            {
                WebResponse webResponse = await request.GetResponseAsync();
                using (Stream stream = webResponse.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        var json = JsonArray.Parse(result);
                        XYPos cur = new XYPos();
                        cur.Floor = (int)json.GetNumberAt(2);
                        cur.X = json.GetNumberAt(0);
                        cur.Y = json.GetNumberAt(1);
                        return cur;
                    }
            }
            catch (Exception)
            {
                new Windows.UI.Popups.MessageDialog("从服务器取回数据出错，程序将退出！").ShowAsync();
                Windows.UI.Xaml.Application.Current.Exit();
                return null;

            }
        }
        public static bool IsOffset(XYPos lastPoint, XYPos nextPoint, XYPos curPos)//判断当前位置是否偏离当前点段
        {
            double x1, y1, x2, y2;
            if (lastPoint.Y < nextPoint.Y)
            {
                x1 = lastPoint.X;
                y1 = lastPoint.Y;
                x2 = nextPoint.X;
                y2 = nextPoint.Y;
            }
            else
            {
                x2 = lastPoint.X;
                y2 = lastPoint.Y;
                x1 = nextPoint.X;
                y1 = nextPoint.Y;
            }
            double x = curPos.X;
            double y = curPos.Y;
            double threshold = 0.05;//阈值
            if (x1 == x2)//竖直区域
            {
                if (x <= (x1 - threshold) || x >= (x1 + threshold) || y > (y2 + threshold) || y < (y1 - threshold))
                    return true;
                else
                    return false;
            }
            else if (y1 == y2)//水平区域
            {
                if (y <= (y1 - threshold) || y >= (y1 + threshold) || (x > (x1 + threshold) && x > (x2 + threshold)) || (x < (x1 - threshold) && x < (x2 - threshold)))
                    return true;
                else
                    return false;
            }
            else//其他类型区域
            {
                double k1 = (y1 - y2) / (x1 - x2), px, py;
                double length = Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
                for (double i = -0.03; i < length + 0.03; i += 0.03)
                {
                    if (k1 > 0)
                    {
                        px = x1 + i / Math.Sqrt(1 + k1 * k1);
                        py = y1 + i * (k1 / Math.Sqrt(1 + k1 * k1));
                    }
                    else
                    {
                        px = x1 - i / Math.Sqrt(1 + k1 * k1);
                        py = y1 - i * (k1 / Math.Sqrt(1 + k1 * k1));
                    }
                    if ((px - x) * (px - x) + (py - y) * (py - y) <= threshold * threshold)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
        public static int GetLateKeyPoint(double x, double y, int floor)//得到当前坐标最近的关键点
        {
            int region = GetDivision(x, y, floor);
            DBItem startPoint = new DBItem();
            double length;
            switch (region)
            {
                case 21:
                    length = double.MaxValue;
                    foreach (DBItem item in DataBase.Point.GetRange(0, 19))
                    {
                        if (((x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y)) < length)
                        {
                            length = (x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y);
                            startPoint = item;
                        }
                    }
                    break;
                case 22:
                    length = double.MaxValue;
                    foreach (DBItem item in DataBase.Point.GetRange(67, 38))
                    {
                        if (((x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y)) < length)
                        {
                            length = (x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y);
                            startPoint = item;
                        }
                    }
                    break;
                case 11:
                    length = double.MaxValue;
                    foreach (DBItem item in DataBase.Point.GetRange(142, 33))
                    {
                        if (((x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y)) < length)
                        {
                            length = (x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y);
                            startPoint = item;
                        }
                    }
                    break;
                case 12:
                    length = double.MaxValue;
                    foreach (DBItem item in DataBase.Point.GetRange(239, 24))
                    {
                        if (((x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y)) < length)
                        {
                            length = (x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y);
                            startPoint = item;
                        }
                    }
                    break;
                case 0:
                    length = double.MaxValue;
                    foreach (DBItem item in DataBase.Point.GetRange(292, 4))
                    {
                        if (((x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y)) < length)
                        {
                            length = (x - item.X) * (x - item.X) + (y - item.Y) * (y - item.Y);
                            startPoint = item;
                        }
                    }
                    break;
            }
            return startPoint.Id;
        }
        public static bool IsReachPoint(XYPos finishPoint, XYPos curPos)//判断是否到达指定点附近
        {
            if ((curPos.X - finishPoint.X) * (curPos.X - finishPoint.X) + (curPos.Y - finishPoint.Y) * (curPos.Y - finishPoint.Y) <= 0.0009 && finishPoint.Floor == curPos.Floor)
                return true;
            else
                return false;

        }
    }
}

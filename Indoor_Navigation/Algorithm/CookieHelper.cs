using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace 路径导航.ViewModel
{
    public static class CookieHelper
    {
        public static bool Write(string name, string value)//存入本地缓存
        {
            try
            {
                ApplicationDataContainer localSetting = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSetting.Values[name] = value;
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }
        public static string Read(string name)//读取本地存储
        {
            ApplicationDataContainer localSetting = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSetting.Values.ContainsKey(name))
            {
                string value = localSetting.Values[name].ToString();
                return value;
            }
            else
                return null;
        }
        public static bool DeleteCookie(string name)//删除本地缓存
        {
            ApplicationDataContainer localSetting = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSetting.Values.ContainsKey(name))
            {
                localSetting.Values.Remove(name);
                return true;
            }
            else
                return false;
        }
    }
}

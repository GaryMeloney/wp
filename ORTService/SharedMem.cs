using System;
using System.Collections.Concurrent;

namespace ORTService
{
    public class DeviceCommand
    {
        public string deviceID;
        public string command;
    }

    public sealed class SharedMem
    {
        private static object syncRoot = new Object();
        private static volatile ConcurrentDictionary<string, DeviceListener> mDeviceListeners;

        private static ConcurrentDictionary<string, DeviceListener> DeviceListeners {
            get {
                if (mDeviceListeners == null)
                {
                    lock (syncRoot)
                    {
                        if (mDeviceListeners == null)
                            mDeviceListeners = new ConcurrentDictionary<string, DeviceListener>();
                    }
                }

                return mDeviceListeners;
            }
        }

        public static bool Add(string key, DeviceListener s)
        {
            try
            {
                return DeviceListeners.TryAdd(key, s);
            }
            catch (Exception e)
            {
                ORTLog.LogS(string.Format("SharedMem Exception in Add {0}", e.ToString()));
                return false;
            }
        }

        public static bool Remove(string key)
        {
            DeviceListener s;
            try
            {
                return DeviceListeners.TryRemove(key, out s);
            }
            catch (Exception e)
            {
                ORTLog.LogS(string.Format("SharedMem Exception in Remove {0}", e.ToString()));
                return false;
            }
        }

        public static DeviceListener Get(string key)
        {
            if (DeviceListeners.ContainsKey(key))
            {
                return DeviceListeners[key];
            }
            else
            {
                return null;
            }
        }
    }
}
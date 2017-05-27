using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

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
        private static volatile ConcurrentDictionary<string, Socket> mDeviceSockets;

        private static ConcurrentDictionary<string, Socket> DeviceSockets {
            get {
                if (mDeviceSockets == null)
                {
                    lock (syncRoot)
                    {
                        if (mDeviceSockets == null)
                            mDeviceSockets = new ConcurrentDictionary<string, Socket>();
                    }
                }

                return mDeviceSockets;
            }
        }

        public static void Add(string key, Socket s)
        {
            DeviceSockets.TryAdd(key, s);
        }

        public static void Remove(string key)
        {
            Socket s;
            DeviceSockets.TryRemove(key, out s);
        }

        public static Socket Get(string key)
        {
            if (DeviceSockets.ContainsKey(key))
            {
                return DeviceSockets[key];
            }
            else
            {
                return null;
            }
        }
    }
}
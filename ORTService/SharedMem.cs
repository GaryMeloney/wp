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

        public static void Add(string deviceId, Socket s)
        {
            DeviceSockets.TryAdd(deviceId, s);
        }

        public static void Remove(string deviceId)
        {
            Socket s;
            DeviceSockets.TryRemove(deviceId, out s);
        }

        public static Socket Get(string deviceID)
        {
            return DeviceSockets[deviceID];
        }
    }
}
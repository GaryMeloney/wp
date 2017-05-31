using System;
using System.Net;
using System.Net.Sockets;

namespace ORTService
{
    public class ORTDeviceServer : ORTServer
    {
        public ORTDeviceServer(IPAddress serverIP, int port) : base(serverIP, port)
        {

        }

        protected override void ServerThreadStart()
        {
            Socket clientSocket = null;
            TCPSocketListener socketListener = null;
            while (!m_stopServer)
            {
                try
                {
                    clientSocket = m_server.AcceptSocket();
                }
                catch (Exception)
                {
                    // This happens when we shutdown the servive
                    continue;
                }

                try
                {
                    ORTLog.LogS(String.Format("ORTDevice: Connection made {0}", clientSocket.RemoteEndPoint));
                }
                catch (Exception e)
                {
                    ORTLog.LogS(string.Format("ORTDevice Exception {0}", e.ToString()));
                    continue;
                }

                // Create a SocketListener object for the client.
                socketListener = new DeviceListener(clientSocket);

                // Add the socket listener to an array list in a thread safe fashion.
                lock (m_socketListenersList)
                {
                    m_socketListenersList.Add(socketListener);
                }

                // Start communicating with the client in a different thread.
                socketListener.StartSocketListener();
            }
        }
    }
}

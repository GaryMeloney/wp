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
                    // Wait for any client requests and if there is any 
                    // request from any client accept it (Wait indefinitely).
                    clientSocket = m_server.AcceptSocket();
                    ORTLog.LogS(String.Format("ORTDevice: connection made {0}", clientSocket.RemoteEndPoint));

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
                catch (SocketException)
                {
                    m_stopServer = true;
                }
            }
        }
    }
}

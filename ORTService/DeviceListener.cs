using System;
using System.Net.Sockets;
using System.Text;

namespace ORTService
{
    public class DeviceListener : TCPSocketListener
    {
        public DeviceListener(Socket clientSocket) : base(clientSocket)
        {
        }

        protected override void SocketListenerThreadStart()
        {
            Byte[] byteBuffer = new Byte[1024];
            int size = m_clientSocket.Receive(byteBuffer);
            string data = Encoding.ASCII.GetString(byteBuffer, 0, size);

            ORTLog.LogS(String.Format("ORTDevice: Add listener for device={0}", data));
            SharedMem.Add(data, m_clientSocket);

            // Block forever waiting for the connection to terminate
            m_clientSocket.ReceiveTimeout = 500;
            while (!m_stopClient)
            {
                try
                {
                    size = m_clientSocket.Receive(byteBuffer);
                    if (size == 0)
                    {
                        // Indicates the remote side closed the connection
                        break;
                    }
                }
                catch (SocketException e)
                {
                    if (e.ErrorCode == WSAETIMEDOUT)
                    {
                        // Timeout while waiting for shutdown
                        continue;
                    }
                    else
                    {
                        ORTLog.LogS(string.Format("ORTCommand: SocketException ErrorCode={0}", e.ErrorCode));
                        break;
                    }
                }
            }

            ORTLog.LogS(String.Format("ORTDevice: Remove listener for device={0}", data));
            SharedMem.Remove(data);

            ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", m_clientSocket.RemoteEndPoint));

            m_clientSocket.Shutdown(SocketShutdown.Both);
            m_clientSocket.Close();
            m_markedForDeletion = true;
        }
    }
}

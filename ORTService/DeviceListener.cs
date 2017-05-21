using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;

namespace ORTService
{
    public class DeviceListener : TCPSocketListener
    {
        public DeviceListener(Socket clientSocket) : base(clientSocket)
        {
        }

        protected override void SocketListenerThreadStart()
        {
            int size = 0;
            Byte[] byteBuffer = new Byte[1024];

            try
            {
                size = m_clientSocket.Receive(byteBuffer);
                if (size != 0)
                {
                    string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
                    ORTLog.LogS(String.Format("ORTDevice: Add device listener for device={0}", data));

                    SharedMem.Add(data, m_clientSocket);

                    // Block forever waiting for the connection to terminate
                    size = m_clientSocket.Receive(byteBuffer);

                    ORTLog.LogS(String.Format("ORTDevice: Remove device listener for device={0}", data));
                    SharedMem.Remove(data);
                }
            }
            catch (SocketException)
            {
                ORTLog.LogS("ORTDevice: listener thread exception");
                m_stopClient = true;
                m_markedForDeletion = true;
            }

            ORTLog.LogS(String.Format("ORTDevice: connection dropped by client {0}", m_clientSocket.RemoteEndPoint));
            m_clientSocket.Close();
        }
    }
}

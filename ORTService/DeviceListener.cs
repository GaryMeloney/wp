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

            while (!m_stopClient)
            {
                try
                {
                    size = m_clientSocket.Receive(byteBuffer);
                    if (size == 0) break;
                    DateTime m_timeNow; m_timeNow = DateTime.Now;

                    string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
                    ORTLog.LogS(data);

                    string testRealtime = string.Format("realtime {0}-{1}-{2}T{3}:{4}:{5} 8", m_timeNow.Year, m_timeNow.Month, m_timeNow.Day, m_timeNow.Hour, m_timeNow.Minute, m_timeNow.Second);
                    m_clientSocket.Send(Encoding.ASCII.GetBytes(testRealtime));
                }
                catch (SocketException)
                {
                    m_stopClient = true;
                    m_markedForDeletion = true;
                }
            }
            ORTLog.LogS(String.Format("{0}:connection dropped by client", m_clientSocket.RemoteEndPoint));
            m_clientSocket.Close();
        }
    }
}

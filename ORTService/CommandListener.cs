using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace ORTService
{
    public class CommandListener : TCPSocketListener
    {
        public CommandListener(Socket clientSocket) : base(clientSocket)
        {
        }

        protected override void SocketListenerThreadStart()
        {
            int size = 0;
            Byte[] byteBuffer = new Byte[1024];

            m_clientSocket.ReceiveTimeout = 500;
            while (!m_stopClient)
            {
                try
                {
                    size = m_clientSocket.Receive(byteBuffer);
                }
                catch (SocketException)
                {
                    // Timeout while waiting for shutdown
                    continue;
                }

                if (size == 0)
                {
                    Thread.Sleep(500);
                    continue;
                }

                // Get a string representation from the socket buffer
                string data = Encoding.ASCII.GetString(byteBuffer, 0, size);

                // Parse the device ID
                string deviceID = data.Split(null)[0];

                // Parse the command
                int i = data.IndexOf(" ") + 1;
                string command = data.Substring(i);

                // Remove the line feed
                command = Regex.Replace(command, @"\r\n?|\n", "");

                ORTLog.LogS(string.Format("ORTCommand: deviceID={0} command={1}", deviceID, command));

                // Get the cooresponding socket and send the command
                Socket s = SharedMem.Get(deviceID);

                if (s != null)
                {
                    s.Send(Encoding.ASCII.GetBytes(command));
                }
                else
                {
                    ORTLog.LogS(string.Format("ORTCommand: deviceID not found: {0}", deviceID));
                }
            }

            ORTLog.LogS(String.Format("ORTCommand: Connection dropped {0}", m_clientSocket.RemoteEndPoint));

            m_clientSocket.Shutdown(SocketShutdown.Both);
            m_clientSocket.Close();
        }
    }
}

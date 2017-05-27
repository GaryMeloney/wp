using System;
using System.Net.Sockets;
using System.Text;
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

                // Get a string representation from the socket buffer
                string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
                if (!IsValidIpsData(data))
                {
                    ORTLog.LogS(String.Format("ORTCommand: Invalid data={0}", data));
                    break;
                }

                // Parse the customer+device
                string customer = data.Split(null)[1];
                string device = data.Split(null)[2];

                // Parse the command - hacky :/
                int i = data.IndexOf(" ", data.IndexOf(" ", data.IndexOf(" ")+1)+1)+1;
                string command = data.Substring(i);

                // Remove the carriage return and/or line feed
                command = Regex.Replace(command, @"\r\n?|\n", "");

                ORTLog.LogS(string.Format("ORTCommand: customer={0} device={1} command={2}", customer, device, command));
                string key = GetKey(customer, device);

                // Get the cooresponding socket and send the command
                Socket s = SharedMem.Get(key);

                if (s != null)
                {
                    s.Send(Encoding.ASCII.GetBytes(command));
                }
                else
                {
                    ORTLog.LogS(string.Format("ORTCommand: customer device combination not found: {0}", key));
                }
            }

            ORTLog.LogS(String.Format("ORTCommand: Connection dropped {0}", m_clientSocket.RemoteEndPoint));

            m_clientSocket.Shutdown(SocketShutdown.Both);
            m_clientSocket.Close();
            m_markedForDeletion = true;
        }
    }
}

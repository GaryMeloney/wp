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

            try { m_clientSocket.ReceiveTimeout = 500; } catch (Exception) { }
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
                        ORTLog.LogS(string.Format("ORTCommand Exception in Receive {0}", e.ToString()));
                        break;
                    }
                }
                catch (Exception e)
                {
                    ORTLog.LogS(string.Format("ORTCommand Exception in Receive {0}", e.ToString()));
                    break;
                }

                // Get a string representation from the socket buffer
                string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
                if (!IsValidIpsData(data))
                {
                    ORTLog.LogS(String.Format("ORTCommand: Invalid data={0}", data));
                    break;
                }

                string customer = "";
                string device = "";
                string command = "";
                try
                {
                    // Parse the customer+device
                    customer = data.Split(null)[1];
                    device = data.Split(null)[2];

                    // Parse the command - hacky :/
                    int i = data.IndexOf(" ", data.IndexOf(" ", data.IndexOf(" ") + 1) + 1) + 1;
                    command = data.Substring(i);

                    // Remove the carriage return and/or line feed
                    command = Regex.Replace(command, @"\r\n?|\n", "");
                }
                catch (Exception)
                {
                    ORTLog.LogS(String.Format("ORTCommand: Invalid data={0}", data));
                    break;
                }

                ORTLog.LogS(string.Format("ORTCommand: customer={0} device={1} command={2}", customer, device, command));
                string key = GetKey(customer, device);

                // Get the cooresponding socket and send the command
                DeviceListener d = SharedMem.Get(key);
                if (d != null)
                {
                    try
                    {
                        if (d.Send(Encoding.ASCII.GetBytes(command)) == 0) break;
                    }
                    catch (Exception e)
                    {
                        ORTLog.LogS(string.Format("ORTCommand Exception {0}", e.ToString()));
                        break;
                    }
                }
                else
                {
                    ORTLog.LogS(string.Format("ORTCommand: customer device combination not found: {0}", key));
                }
            }

            ORTLog.LogS(String.Format("ORTCommand: Connection dropped {0}", this.ToString()));

            try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            m_clientSocket.Close();
            m_markedForDeletion = true;
        }
    }
}

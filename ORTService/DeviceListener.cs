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

        public int Send(byte[] buffer)
        {
            return m_clientSocket.Send(buffer);
        }

        protected override void SocketListenerThreadStart()
        {
            Byte[] byteBuffer = new Byte[1024];
            int size = 0;
            try
            {
                size = m_clientSocket.Receive(byteBuffer);
            }
            catch (Exception e)
            {
                ORTLog.LogS(string.Format("ORTDevice Exception {0}", e.ToString()));
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
            if (!IsValidIpsData(data))
            {
                ORTLog.LogS(String.Format("ORTDevice: Invalid data={0}", data));
                ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));
                try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            string customer = "";
            string device = "";
            try
            {
                // Parse the customer+device
                customer = data.Split(null)[1];
                device = data.Split(null)[2];
            }
            catch (Exception)
            {
                ORTLog.LogS(String.Format("ORTDevice: Invalid data={0}", data));
                ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));
                try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            string key = GetKey(customer, device);

            // Check if this key already exists
            DeviceListener d = SharedMem.Get(key);
            if (d != null)
            {
                // Remove existing key
                ORTLog.LogS(String.Format("ORTDevice: Detected duplicate customer={0} device={1}", customer, device));
                d.StopSocketListener();
            }

            ORTLog.LogS(String.Format("ORTDevice: Add listener for customer={0} device={1}", customer, device));
            if (!SharedMem.Add(key, this))
            {
                ORTLog.LogS(String.Format("ORTDevice: Unable to add key={0}", key));
                ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));
                try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            // Block forever waiting for the connection to terminate
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
                        ORTLog.LogS(string.Format("ORTDevice Exception {0}", e.ToString()));
                        break;
                    }
                }
                catch (Exception e)
                {
                    ORTLog.LogS(string.Format("ORTDevice Exception {0}", e.ToString()));
                    break;
                }
            }

            ORTLog.LogS(String.Format("ORTDevice: Remove listener for customer={0} device={1}", customer, device));
            SharedMem.Remove(key);

            ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));

            try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            m_clientSocket.Close();
            m_markedForDeletion = true;
        }
    }
}

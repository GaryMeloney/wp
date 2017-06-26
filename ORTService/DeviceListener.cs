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
            int ret = 0;

            try
            {
                ret = m_clientSocket.Send(buffer);
            }
            catch (Exception e)
            {
                ORTLog.LogS(string.Format("ORTDevice Exception in Send {0}", e.ToString()));
            }

            return ret;
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
                ORTLog.LogS(string.Format("ORTDevice Exception in Receive {0}", e.ToString()));
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            string data = Encoding.ASCII.GetString(byteBuffer, 0, size);
            if (!IsValidIpsData(data))
            {
                ORTLog.LogS(String.Format("ORTDevice: Invalid data={0}", CleanString(data)));
                ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));
                try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
                m_clientSocket.Close();
                m_markedForDeletion = true;
                return;
            }

            string customer = "";
            string device = "";
            string ipAddr = "";
            try
            {
                // Parse the customer+device
                customer = data.Split(null)[1];
                device = data.Split(null)[2];
                ipAddr = data.Split(null)[3];
            }
            catch (Exception)
            {
                ORTLog.LogS(String.Format("ORTDevice: Invalid data={0}", CleanString(data)));
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
                // Detected duplicate
                ORTLog.LogS(String.Format("ORTDevice: Detected duplicate customer={0} device={1}", customer, device));

                // Remove existing key
                ORTLog.LogS(String.Format("ORTDevice: Remove listener for customer={0} device={1}", customer, device));
                SharedMem.Remove(key);

                // Shutdown the existing (zombie) listener
                d.StopSocketListener();
            }

            ORTLog.LogS(String.Format("ORTDevice: Add listener for customer={0} device={1} localIpAddr={2}", customer, device, ipAddr));
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
            while (!m_stopClient)
            {
                bool poll = false;
                try
                {
                    poll = m_clientSocket.Poll(100000, SelectMode.SelectRead); // 100ms
                }
                catch (Exception e)
                {
                    ORTLog.LogS(string.Format("ORTDevice Exception in Poll {0}", e.ToString()));
                    break;
                }

                if (poll)
                {
                    // Connection has been closed, reset, or terminated
                    break;
                }
            }

            if (SharedMem.Remove(key))
            {
                ORTLog.LogS(String.Format("ORTDevice: Removed listener for customer={0} device={1}", customer, device));
            }

            ORTLog.LogS(String.Format("ORTDevice: Connection dropped {0}", this.ToString()));

            try { m_clientSocket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            m_clientSocket.Close();
            m_markedForDeletion = true;
        }
    }
}
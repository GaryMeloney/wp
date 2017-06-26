using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ORTService
{
    public abstract class TCPSocketListener
    {
        protected Socket m_clientSocket = null;
        protected bool m_stopClient = false;
        protected Thread m_clientListenerThread = null;
        protected bool m_markedForDeletion = false;
        private string m_remoteEndPoint;

        protected const int WSAETIMEDOUT = 10060;
        private const string IPS_TOKEN = "IPS";

        public TCPSocketListener(Socket clientSocket)
        {
            m_clientSocket = clientSocket;
        }

        ~TCPSocketListener()
        {
            StopSocketListener();
        }

        protected abstract void SocketListenerThreadStart();

        public void StartSocketListener()
        {
            if (m_clientSocket == null)
            {
                m_markedForDeletion = true;
                return;
            }

            try
            {
                m_remoteEndPoint = m_clientSocket.RemoteEndPoint.ToString();
            }
            catch (Exception e)
            {
                ORTLog.LogS(string.Format("TCPSocketListener Exception {0}", e.ToString()));
                m_markedForDeletion = true;
                return;
            }

            m_clientListenerThread = new Thread(new ThreadStart(SocketListenerThreadStart));
            m_clientListenerThread.Start();
        }

        public void StopSocketListener()
        {
            if (m_clientSocket == null)
            {
                return;
            }

            // Set flag to tell thread to shutdown
            m_stopClient = true;

            // Wait for one second for the the thread to stop
            m_clientListenerThread.Join(1000);

            // If still alive; Get rid of the thread
            if (m_clientListenerThread.IsAlive)
            {
                m_clientListenerThread.Abort();
            }

            m_clientListenerThread = null;
            m_clientSocket = null;
            m_markedForDeletion = true;
        }

        public bool IsMarkedForDeletion()
        {
            return m_markedForDeletion;
        }

        public override string ToString()
        {
            return m_remoteEndPoint;
        }

        protected bool IsValidIpsData(string strRceived)
        {
            if (strRceived.Length < IPS_TOKEN.Length) return false;
            string token = strRceived.Substring(0, IPS_TOKEN.Length).ToUpper();
            return token.CompareTo(IPS_TOKEN) == 0;
        }

        protected string GetKey(string customer, string device)
        {
            return string.Format("{0}.{1}", customer, device);
        }

        // Remove non-printable characters from given string; this is useful for logfiles
        // Symbols are also removed such as: currency symbols, number forms, subscripts and superscripts, 
        // mathematical operators and arrows, geometric symbols, technical symbols, braille patterns, and dingbats.
        static public string CleanString(string s)
        {
            if (s != null && s.Length > 0)
            {
                StringBuilder sb = new StringBuilder(s.Length);
                foreach (char c in s)
                {
                    if (Char.IsSymbol(c))
                    {
                        continue;
                    }
                    sb.Append(Char.IsLetterOrDigit(c) ? c : ' ');
                }
                s = sb.ToString();
            }
            return s;
        }
    }
}

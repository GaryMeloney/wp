using System.Net.Sockets;
using System.Threading;

namespace ORTService
{
    public abstract class TCPSocketListener
    {
        protected Socket m_clientSocket = null;
        protected bool m_stopClient = false;
        protected Thread m_clientListenerThread = null;
        protected bool m_markedForDeletion = false;

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
            if (m_clientSocket != null)
            {
                m_clientListenerThread = new Thread(new ThreadStart(SocketListenerThreadStart));
                m_clientListenerThread.Start();
            }
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
    }
}

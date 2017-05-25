using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ORTService
{
    public abstract class ORTServer
    {
        protected TcpListener m_server = null;
        protected bool m_stopServer = false;
        protected bool m_stopPurging = false;
        protected Thread m_serverThread = null;
        protected Thread m_purgingThread = null;
        protected ArrayList m_socketListenersList = null;

        public ORTServer(IPAddress serverIP, int port)
        {
            Init(new IPEndPoint(serverIP, port));
        }

        ~ORTServer()
        {
            StopServer();
        }

        protected abstract void ServerThreadStart();

        private void Init(IPEndPoint ipNport)
        {
            try
            {
                m_server = new TcpListener(ipNport);
            }
            catch (Exception)
            {
                m_server = null;
            }
        }

        public void StartServer()
        {
            if (m_server != null)
            {
                m_socketListenersList = new ArrayList();

                m_server.Start();
                m_serverThread = new Thread(new ThreadStart(ServerThreadStart));
                m_serverThread.Start();

                // Create a low priority thread that checks and deletes client
                // SocktConnection objects that are marked for deletion.
                m_purgingThread = new Thread(new ThreadStart(PurgingThreadStart));
                m_purgingThread.Priority = ThreadPriority.Lowest;
                m_purgingThread.Start();
            }
        }

        public void StopServer()
        {
            if (m_server != null)
            {
                // It is important to Stop the server first before doing
                // any cleanup. If not so, clients might being added as
                // server is running, but supporting data structures
                // (such as m_socketListenersList) are cleared. This might
                // cause exceptions.

                // Stop the TCP/IP Server.
                m_stopServer = true;
                m_server.Stop();

                // Wait for one second for the the thread to stop.
                m_serverThread.Join(1000);

                // If still alive; Get rid of the thread.
                if (m_serverThread.IsAlive)
                {
                    m_serverThread.Abort();
                }
                m_serverThread = null;

                m_stopPurging = true;
                m_purgingThread.Join(1000);
                if (m_purgingThread.IsAlive)
                {
                    m_purgingThread.Abort();
                }
                m_purgingThread = null;

                // Free Server Object.
                m_server = null;

                // Stop All clients.
                StopAllSocketListers();
            }
        }

        private void StopAllSocketListers()
        {
            lock (m_socketListenersList)
            {
                foreach (TCPSocketListener socketListener in m_socketListenersList)
                {
                    socketListener.StopSocketListener();
                }
            }

            m_socketListenersList.Clear();
            m_socketListenersList = null;
        }

        private void CheckForThreadsToPurge()
        {
            ArrayList deleteList = new ArrayList();

            // Check for any clients SocketListeners that are to be
            // deleted and put them in a separate list in a thread safe fashion.
            lock (m_socketListenersList)
            {
                foreach (TCPSocketListener socketListener in m_socketListenersList)
                {
                    if (socketListener.IsMarkedForDeletion())
                    {
                        ORTLog.LogD(string.Format("PurgingThread: StopSocketListener {0}", socketListener.ToString()));
                        deleteList.Add(socketListener);
                        socketListener.StopSocketListener();
                    }
                }

                // Delete all the client SocketConnection ojects which are
                // in marked for deletion and are in the delete list.
                for (int i = 0; i < deleteList.Count; ++i)
                {
                    m_socketListenersList.Remove(deleteList[i]);
                }
            }

            deleteList = null;
        }

        /// <summary>
        /// Thread method for purging Client Listeneres that are marked for
        /// deletion (i.e. clients with socket connection closed). This thead
        /// is a low priority thread and sleeps for 10 seconds and then check
        /// for any client SocketConnection obects which are obselete and 
        /// marked for deletion.
        /// </summary>
        private void PurgingThreadStart()
        {
            ORTLog.LogD("PurgingThread: Start");

            while (!m_stopPurging)
            {
                CheckForThreadsToPurge();

                // Wait 10 seconds, but check for shutdown every 100ms
                int sleep = 10000;
                while (!m_stopPurging && sleep > 0)
                {
                    Thread.Sleep(100);
                    sleep -= 100;
                }
            }

            ORTLog.LogD("PurgingThread: Stop");
        }
    }
}

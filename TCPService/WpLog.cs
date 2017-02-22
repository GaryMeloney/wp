using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TCPService
{
    public static class WpLog
    {
        private static readonly object Locker = new object();
        private static StreamWriter m_logDebug;
        private static StreamWriter m_logSession;
        private const string DEBUG_LOG_FILENAME = "C:\\wp_debug.txt";
        private const string SESION_LOG_FILENAME = "C:\\wp_session.txt";

        public static bool EnableDebug { get; set; } = false;
        public static bool EnableSession { get; set; } = false;

        public static void Open()
        {
            m_logDebug = File.Exists(DEBUG_LOG_FILENAME) ? File.AppendText(DEBUG_LOG_FILENAME) : new StreamWriter(DEBUG_LOG_FILENAME);
            m_logDebug.AutoFlush = true;

            m_logSession = File.Exists(SESION_LOG_FILENAME) ? File.AppendText(SESION_LOG_FILENAME) : new StreamWriter(SESION_LOG_FILENAME);
            m_logSession.AutoFlush = true;
        }

        public static void Close()
        {
            if (m_logDebug != null)
            {
                m_logDebug.Close();
            }

            if (m_logSession != null)
            {
                m_logSession.Close();
            }
        }

        public static void LogD(string msg)
        {
            if (!EnableDebug) return;

            lock (Locker)
            {
                int epoch = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                m_logDebug.WriteLine(epoch + " " + msg);
            }
        }

        public static void LogS(string msg)
        {
            if (!EnableSession) return;

            lock (Locker)
            {
                int epoch = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                m_logSession.WriteLine(epoch + " " + msg);
            }
        }
    }
}
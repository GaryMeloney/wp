/*
 * Reference this link for original source:
 * https://www.codeproject.com/Articles/5733/A-TCP-IP-Server-written-in-C
 * 
 * Helpful commands to execute *AS ADMINISTSTATOR* from the Visual Studio Developer Command Prompt
 * 
 * sc create ORTService binpath= "C:\Users\miked\Documents\GitHub\wp\ORTService\bin\Release\ORTService.exe" displayname= "ORTService" depend= Tcpip start= auto 
 * sc delete ORTService
 * sc start ORTService -d -s
 * sc stop ORTService
 * 
 */

using System.Net;
using System.Configuration;
using System.Collections.Specialized;

namespace ORTService
{
    public class ORTService : System.ServiceProcess.ServiceBase
    {
        private System.ComponentModel.Container components = null;
        private ORTDeviceServer m_deviceServer = null;
        private ORTCommandServer m_commandServer = null;

        public ORTService()
        {
            InitializeComponent();
        }

        static void Main(string[] args)
        {
            System.ServiceProcess.ServiceBase[] ServicesToRun;
            ServicesToRun = new System.ServiceProcess.ServiceBase[] { new ORTService() };
            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            this.ServiceName = "ORTService";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnStart(string[] args)
        {
            var appSettings = ConfigurationManager.AppSettings;
            var LogFlags = ConfigurationManager.GetSection("LogFlags") as NameValueCollection;
            string debugFilename = @"C:\cygwin64\home\listdog\logs\ort_debug.txt";
            string sessionFilename = @"C:\cygwin64\home\listdog\logs\ort_session.txt";

            if (LogFlags != null)
            {
                if (LogFlags["LogDebug"] != null &&
                    string.Compare(LogFlags["LogDebug"].ToString(), "1", true) == 0)
                {
                    ORTLog.EnableDebug = true;
                }

                if (LogFlags["LogSession"] != null &&
                        string.Compare(LogFlags["LogSession"].ToString(), "1", true) == 0)
                {
                    ORTLog.EnableSession = true;
                }

                if (LogFlags["DebugFilename"] != null)
                {
                    debugFilename = LogFlags["DebugFilename"].ToString();
                }

                if (LogFlags["SessionFilename"] != null)
                {
                    sessionFilename = LogFlags["SessionFilename"].ToString();
                }
            }

            ORTLog.Open(debugFilename, sessionFilename);

            var TcpFlags = ConfigurationManager.GetSection("LogFlags") as NameValueCollection;
            int deviceServerPort = 3333;
            int commandServerPort = 8888;
            if (TcpFlags != null)
            {
                if (TcpFlags["ORTDeviceServerPort"] != null)
                {
                    deviceServerPort = int.Parse(TcpFlags["ORTDeviceServerPort"].ToString());
                }

                if (TcpFlags["ORTCommandServerPort"] != null)
                {
                    commandServerPort = int.Parse(TcpFlags["ORTCommandServerPort"].ToString());
                }
            }

            m_deviceServer = new ORTDeviceServer(IPAddress.Any, deviceServerPort);
            m_deviceServer.StartServer();

            m_commandServer = new ORTCommandServer(IPAddress.Any, commandServerPort);
            m_commandServer.StartServer();

            ORTLog.LogD("Service Started");
        }

        protected override void OnStop()
        {
            ORTLog.LogD("Service Stopped");
            ORTLog.Close();

            m_deviceServer.StopServer();
            m_deviceServer = null;

            m_commandServer.StopServer();
            m_commandServer = null;
        }
    }
}
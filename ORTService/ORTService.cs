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
            ORTLog.Open();

            foreach (string s in args)
            {
                if (s.Equals("-d")) ORTLog.EnableDebug = true;
                if (s.Equals("-s")) ORTLog.EnableSession = true;
            }

            m_deviceServer = new ORTDeviceServer(IPAddress.Any, 3333);
            m_deviceServer.StartServer();

            m_commandServer = new ORTCommandServer(IPAddress.Any, 8888);
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
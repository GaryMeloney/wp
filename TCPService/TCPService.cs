using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

/*
 * Reference this link for original source:
 * https://www.codeproject.com/Articles/5733/A-TCP-IP-Server-written-in-C
 * 
 * Helpful commands to execute *AS ADMINISTSTATOR* from the Visual Studio Developer Command Prompt
 * 
 * sc create TCPService binpath= "C:\Users\miked\Documents\GitHub\wp\TCPService\bin\Release\TCPService.exe" displayname= "WaterPigeonServer" depend= Tcpip start= auto 
 * sc delete TCPService
 * sc start TCPService -d -s
 * sc stop TCPService
 * 
 */

namespace TCPService
{
	/// <summary>
	/// Class that will run as a Windows Service and its display name is
	/// TCP (Sabre Group Config Transfer Service) in Windows Services.
	/// This service basically start a server on service start 
	/// (on OnStart method) and shutdown the server on the servie stop 
	/// (on OnStop method).
	/// </summary>
	public class TCPService : System.ServiceProcess.ServiceBase
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private TCPServer server = null;

		public TCPService()
		{
			// This call is required by the Windows.Forms Component Designer.
			InitializeComponent();
		}

		// The main entry point for the process
		static void Main(string[] args)
		{
            System.ServiceProcess.ServiceBase[] ServicesToRun;
	
			// More than one user Service may run within the same process. To add
			// another service to this process, change the following line to
			// create a second service object. For example,
			//
			//   ServicesToRun = new System.ServiceProcess.ServiceBase[] {new TCPService(), new MySecondUserService()};
			//
			ServicesToRun = new System.ServiceProcess.ServiceBase[] { new TCPService() };

			System.ServiceProcess.ServiceBase.Run(ServicesToRun);
		}

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
			this.ServiceName = "TCPService";
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		/// <summary>
		/// Set things in motion so your service can do its work.
		/// </summary>
		protected override void OnStart(string[] args)
		{
            WpLog.Open();
            
            //int count = 0;
            foreach (string s in args)
            {
                if (s.Equals("-d")) WpLog.EnableDebug = true;
                if (s.Equals("-s")) WpLog.EnableSession = true;

                //WpLog.LogD(String.Format("arg {0} {1}", count++, s));
            }

            WpLog.LogD("Service Started");

            // Create the Server Object and Start it.
            server = new TCPServer();
            server.StartServer();
		}
 
		/// <summary>
		/// Stop this service.
		/// </summary>
		protected override void OnStop()
		{
            WpLog.LogD("Service Stopped");
            WpLog.Close();

            // Stop the Server. Release it.
            server.StopServer();
			server = null;
        }
    }
}
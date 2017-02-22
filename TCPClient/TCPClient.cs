using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TCPClient
{
    class TCPClient
	{
		[STAThread]
		static void Main(string[] args)
		{
			TCPClient client = null;
			client = new TCPClient();
            client = new TCPClient();
            client = new TCPClient();
            client = new TCPClient();
            client = new TCPClient();
        }

		public TCPClient()
		{
			Thread t = new Thread(new ThreadStart(ClientThreadStart));
			t.Start();
		}

        private void ClientThreadStart()
        {
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 31001));
            clientSocket.Send(Encoding.ASCII.GetBytes("11223344:1487609000:6653"));

            //Thread.Sleep(1000000);

            clientSocket.Disconnect(false);
            clientSocket.Close();
        }
	}
}

using System;
using System.Net;
using System.Net.Sockets;

namespace ConnectionMonitor.Logic
{
    public class ClientConnection
    {
        public TcpClient Client { get; }

        public DateTime Accepted { get; }

        public string Address { get; }

        public ClientConnection(TcpClient tcpClient, DateTime accepted)
        {
            
            this.Client = tcpClient;
            this.Accepted = accepted;
            var endPoint = Client.Client.RemoteEndPoint as IPEndPoint;
            if (endPoint !=null)
            {
                this.Address = endPoint.ToString();
            }
        }

        public void Close()
        {
            Client.Close();            
        }
    }
}
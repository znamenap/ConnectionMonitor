using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ConnectionMonitor.Logic
{
    public class TcpServer : IDisposable
    {
        private readonly ILog log;
        private readonly TcpListener listener;

        public TcpServer(ILog log, string address, int port)
        {
            this.log = log;
            var ipAddress = address == "0.0.0.0"
                ? Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].MapToIPv4()
                : IPAddress.Parse(address);

            listener = new TcpListener(ipAddress, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
            listener.AllowNatTraversal(true);
            listener.Start(10);
            log.InfoFormat("LSTN: {0}", listener.LocalEndpoint.ToString("0G"));
        }

        public async Task<PingPongConnection> ListenAsync()
        {
            var client = await listener.AcceptTcpClientAsync();
            var connection = new PingPongConnection(log, client, DateTime.UtcNow);
            return connection;
        }

        public void Dispose()
        {
            listener.Stop();
        }
    }
}

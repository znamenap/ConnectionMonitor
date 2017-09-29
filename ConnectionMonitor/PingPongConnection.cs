using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace ConnectionMonitor
{
    public static class EndPointExtension
    {
        public static string ToString(this EndPoint endPoint, string format)
        {
            if (endPoint == null)
            {
                return null;
            }
            var ipEndPoint = endPoint as IPEndPoint;
            if (ipEndPoint?.AddressFamily == AddressFamily.InterNetwork)
            {
                if (format.Equals("0G", StringComparison.OrdinalIgnoreCase))
                {
                    var address = ipEndPoint.Address.GetAddressBytes();
                    return $"{address[0]:D3}.{address[1]:D3}.{address[2]:D3}.{address[3]:D3}:{ipEndPoint.Port,-5:D}";
                }
            }

            return endPoint.ToString();
        }
    }
    public class PingPongConnection
    {
        private readonly ILog log;
        private readonly TcpClient connection;
        private readonly Encoding encoding;
        private readonly int maxErrorsToBailOn;

        public DateTime Oppenned { get; private set; }

        public string RemoteEndPoint { get; private set; }

        public string LocalEndPoint { get; private set; }

        public PingPongConnection(ILog log, string remoteAddress, int remotePort)
            : this(log, Encoding.UTF8)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
            RemoteEndPoint = remoteEndPoint.ToString("0G");
            connection = new TcpClient();
            SetupConnectionDetails();

            Open(remoteEndPoint);
        }

        public PingPongConnection(ILog log, TcpClient connection, DateTime oppenned)
            : this(log, Encoding.UTF8)
        {
            this.connection = connection;
            SetupConnectionDetails();

            Oppenned = oppenned;
            RemoteEndPoint = connection.Client.RemoteEndPoint.ToString("0G");
            LocalEndPoint = connection.Client.LocalEndPoint.ToString("0G");
        }

        private PingPongConnection(ILog log, Encoding encoding)
        {
            this.log = log;
            this.encoding = encoding;
            maxErrorsToBailOn = 5;
            LocalEndPoint = "000.000.000.000:*****";
            RemoteEndPoint = "000.000.000.000:*****";
        }

        public void Open(IPEndPoint remoteEndPoint)
        {
            if (connection.Connected)
            {
                throw new InvalidOperationException($"Connection to {RemoteEndPoint} is already open and cannot be used for {remoteEndPoint}.");
            }

            log.InfoFormat("Connecting to {0}", remoteEndPoint);
            connection.Connect(remoteEndPoint);
            SetupConnectionDetails();
            Oppenned = DateTime.UtcNow;
            RemoteEndPoint = connection.Client.RemoteEndPoint.ToString("0G");
            LocalEndPoint = connection.Client.LocalEndPoint.ToString("0G");

            if (!connection.Connected)
            {
                throw new InvalidOperationException($"Cannot connect to {RemoteEndPoint}");
            }
            this.log.InfoFormat("Connected from '{0}' to '{1}'.", LocalEndPoint, RemoteEndPoint);
        }

        public void Close()
        {
            if (connection.Connected)
            {
                log.InfoFormat("Closing connection {0}", this);
                connection.Close();
            }
        }

        public override string ToString()
        {
            return $"@{Oppenned:O}: {LocalEndPoint} -> {RemoteEndPoint}";
        }

        public async Task PlayPingPongAsync(CancellationToken cancellationToken)
        {
            if (!connection.Connected)
            {
                throw new SocketException((int) SocketError.NotConnected);
            }
            using (var stream = connection.GetStream())
            {
                var errors = 0;
                var iteration = 0;
                var buffer = new byte[1024];
                log.InfoFormat("Started ping pong from {0} to {1}", LocalEndPoint, RemoteEndPoint);
                while (!cancellationToken.IsCancellationRequested && errors < maxErrorsToBailOn)
                {
                    try
                    {
                        iteration++;
                        var count = await SendPingAsync(buffer, stream, iteration, cancellationToken);
                        Array.Clear(buffer, 0, count);

                        count = await ReceivePongAsync(buffer, stream, iteration, cancellationToken);
                        Array.Clear(buffer, 0, count);
                        cancellationToken.WaitHandle.WaitOne(2000);

                        errors = 0;
                    }
                    catch (AggregateException e)
                    {
                        errors++;
                        log.DebugFormat("While pinging received error: {0}", e.Flatten().GetType().Name);
                    }
                    catch (Exception e)
                    {
                        errors++;
                        log.DebugFormat("While pinging received error: {0}", e.GetType().Name);
                    }
                    if (errors >= maxErrorsToBailOn)
                    {
                        log.WarnFormat($"Bailing out of ping pong play as errors reached maximum at {maxErrorsToBailOn}.");
                    }
                }
            }
        }

        private async Task<int> SendPingAsync(byte[] buffer, NetworkStream stream, int iteration, CancellationToken cancellationToken)
        {
            try
            {
                var message = $"{{UtcDateTime:'{DateTime.UtcNow:O}', Iteration:{iteration}}},";
                var count = encoding.GetBytes(message, 0, Math.Min(buffer.Length, message.Length), buffer, 0);
                log.InfoFormat("SEND: {0} -> {1} : {2}",
                        LocalEndPoint, RemoteEndPoint, message);
                await stream.WriteAsync(buffer, 0, count, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                return count;
            }
            catch (Exception e)
            {
                log.WarnFormat("SEND: {0} -> {1} : Failed : {2} : {3}",
                        LocalEndPoint, RemoteEndPoint, e.GetType().Name, e.Message);
                throw;
            }
        }

        private async Task<int> ReceivePongAsync(byte[] buffer, NetworkStream stream, int iteration, CancellationToken cancellationToken)
        {
            try
            {
                var count = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, connection.Available), cancellationToken);
                if (count == 0)
                {
                    log.WarnFormat(" EXP: {0} -> {1} : timeout", RemoteEndPoint, LocalEndPoint);
                }
                else
                {
                    log.InfoFormat("RECV: {0} -> {1} : {2}", 
                        RemoteEndPoint, LocalEndPoint, encoding.GetString(buffer, 0, count));
                }
                await stream.FlushAsync(cancellationToken);
                return count;
            }
            catch (Exception e)
            {
                log.WarnFormat("RECV: {0} -> {1} : Failed : {2} : {3}",
                        RemoteEndPoint, LocalEndPoint, e.GetType().Name, e.Message);
                throw;
            }
        }

        private void SetupConnectionDetails()
        {
            connection.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
            connection.ReceiveTimeout = 2000;
        }
    }
}
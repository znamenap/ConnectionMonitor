using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConnectionMonitor.Logic;
using log4net;

namespace ConnectionMonitor
{
    public class IncomingConnectionMonitor : IDisposable
    {
        private readonly ILog log;
        private readonly BlockingCollection<PingPongConnection> connections;
        private readonly Task respondingFeature;
        private readonly List<Task> pongFeatures;
        private readonly CancellationToken cancellationToken;

        public IncomingConnectionMonitor(ILog log, CancellationToken cancellationToken)
        {
            this.log = log;
            this.cancellationToken = cancellationToken;
            this.connections = new BlockingCollection<PingPongConnection>();
            this.pongFeatures = new List<Task>();
            this.respondingFeature = Task.Run(() => RespondConnections(), this.cancellationToken);
        }

        public async Task MonitorAsync(string address, int port)
        {
            using (var server = new TcpServer(log, address, port))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await server.ListenAsync()
                            .ContinueWith(RegisterConnection, null, cancellationToken);
                    }
                    catch (TaskCanceledException taskCancelException)
                    {
                        log.Debug("Task cancelled", taskCancelException);
                    }
                    catch (AggregateException ae)
                    {
                        log.Error("Acceptance of incomming connection failed", ae.Flatten());
                    }
                    catch (Exception e)
                    {
                        log.Error("Acceptance of incomming connection failed", e);
                    }
                }
            }
        }

        private void RegisterConnection(Task<PingPongConnection> listenFeature, object state)
        {
            var connection = listenFeature.Result;
            log.InfoFormat("CONN: {0} <- {1}: Success at {2}",
                connection.LocalEndPoint, connection.RemoteEndPoint, connection.Oppenned);

            if (!connections.TryAdd(connection, 1000, cancellationToken))
            {
                log.Error("Failed to enqueue client connection.");
            }
        }

        private void RespondConnections()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = connections.Take(cancellationToken);

                var pongFeature = Task.Run(() => RunPingPongConnectionAsync(connection), cancellationToken);
                pongFeatures.Add(pongFeature);
            }
        }

        private async void RunPingPongConnectionAsync(PingPongConnection connection)
        {
            try
            {
                await connection.PlayPingPongAsync(cancellationToken);
            }
            finally
            {
                connection.Close();
            }
        }

        public void Dispose()
        {
            pongFeatures.ForEach(t =>
            {
                if (t.IsFaulted)
                {
                    log.Error("Pong feature failed", t.Exception.Flatten());
                }
            });
            if (respondingFeature.IsFaulted)
            {
                log.Error("Responding feature reported failure.", respondingFeature.Exception.Flatten());
            }
        }
    }
}
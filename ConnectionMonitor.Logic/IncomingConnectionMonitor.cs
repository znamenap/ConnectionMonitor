using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace ConnectionMonitor.Logic
{
    public class IncomingConnectionMonitor : IDisposable
    {
        private readonly ILog log;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<ClientConnection> clientConnections;
        private readonly Task respondingFeature;
        private readonly List<Task> pongFeatures;

        public IncomingConnectionMonitor(ILog log, CancellationTokenSource cancellationTokenSource)
        {
            this.log = log;
            this.cancellationTokenSource = cancellationTokenSource;
            this.clientConnections = new BlockingCollection<ClientConnection>();
            this.pongFeatures = new List<Task>();
            this.respondingFeature = Task.Run(() => RespondClientConnections(), this.cancellationTokenSource.Token);
        }

        public async Task MonitorAsync(string address, int port)
        {
            using (var server = new TcpServer(log, address, port))
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await server.ListenAsync()
                            .ContinueWith(RegisterClientConnection, null, cancellationTokenSource.Token);
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

        private void RegisterClientConnection(Task<ClientConnection> listenFeature, object state)
        {
            var connection = listenFeature.Result;
            log.InfoFormat("Client connection accepted at {0} from {1}",
                connection.Accepted, connection.Address);
            if (!clientConnections.TryAdd(connection, 1000, cancellationTokenSource.Token))
            {
                log.Error("Failed to enqueue client connection.");
            }
        }

        private void RespondClientConnections()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var clientConnection = clientConnections.Take(cancellationTokenSource.Token);

                var pongFeature = Task.Run(() => PongConnection(clientConnection), cancellationTokenSource.Token);
                pongFeatures.Add(pongFeature);
            }
        }

        private async void PongConnection(ClientConnection clientConnection)
        {
            var localEndPoint = (IPEndPoint) clientConnection.Client.Client.LocalEndPoint;
            var remoteEndPoint = (IPEndPoint)clientConnection.Client.Client.RemoteEndPoint;
            try
            {
                using (var stream = clientConnection.Client.GetStream())
                {
                    var cancellationToken = cancellationTokenSource.Token;
                    log.InfoFormat("Started pinging from incoming {0} to outgoing {1}", localEndPoint, remoteEndPoint);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var count = 0;
                        var buffer = new byte[1024];
                        try
                        {
                            if (stream.DataAvailable)
                            {
                                Array.Clear(buffer, 0, count);
                                count = stream.Read(buffer, 0, Math.Min(buffer.Length, clientConnection.Client.Available));
                                log.InfoFormat("Received message = {0}", Encoding.UTF8.GetString(buffer, 0, count));
                                stream.FlushAsync(cancellationToken).Wait(cancellationToken);
                            }
                            
                        }
                        catch (Exception e)
                        {
                            log.ErrorFormat("Failed to read data from client {0}. - {1}", clientConnection.Address, e);
                            return;
                        }
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        try
                        {
                            var message = DateTime.UtcNow.ToString("O");
                            Array.Clear(buffer, 0, count);
                            count = Encoding.UTF8.GetBytes(message, 0, message.Length, buffer, 0);
                            log.InfoFormat("Sending message = {0}", Encoding.UTF8.GetString(buffer, 0, count));
                            await stream.WriteAsync(buffer, 0, count, cancellationToken);
                            await stream.FlushAsync(cancellationToken);
                        }
                        catch (Exception e)
                        {
                            log.ErrorFormat("Failed to send data to client {0}. - {1}", clientConnection.Address, e);
                            return;
                        }

                        cancellationToken.WaitHandle.WaitOne(2000);
                    }
                }
            }
            finally
            {
                log.InfoFormat("Closing client connection {0} accepted at {1}", clientConnection.Address,
                    clientConnection.Accepted);
                clientConnection.Close();
            }
        }

        public void Dispose()
        {
            pongFeatures.ForEach(t =>
            {
                if (t.IsFaulted)
                {
                    log.Error("Pong feature failed", t.Exception);
                }
            });
            if (respondingFeature.IsFaulted)
            {
                log.Error("Responding feature reported failure.", respondingFeature.Exception.Flatten());
            }
        }
    }
}
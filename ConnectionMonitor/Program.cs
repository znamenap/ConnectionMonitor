using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using ConnectionMonitor.Logic;
using ConnectionMonitor.Options;
using log4net;

namespace ConnectionMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var log = InitialiseLogging();

            try
            {
                string command = null;
                object obj = null;
                var options = new ConnectionMonitorOptions();
                if (!Parser.Default.ParseArguments(args, options, (s, o) =>
                {
                    command = s;
                    obj = o;
                }))
                {
                    // options.LastParserState.Errors.ToList().ForEach(error => Console.WriteLine(error.BadOption.LongName));
                    Environment.Exit(Parser.DefaultExitCodeFail);
                }

                Console.TreatControlCAsInput = false;
                var cancellationToken = cancellationTokenSource.Token;
                Console.CancelKeyPress += (s, e) => ConsoleCancelKeyPress(s, e, cancellationTokenSource, log);

                if (command == "incomming")
                {
                    var incommingOptions = (IncommingVerbOptions) obj;
                    using (var incomingConnection = new IncomingConnectionMonitor(log, cancellationToken))
                    {
                        var task = incomingConnection.MonitorAsync(incommingOptions.Address, incommingOptions.Port);
                        task.Wait(cancellationToken);
                    }
                }
                else if (command == "outgoing")
                {
                    var outgoingVerbOptions = (OutgoingVerbOptions) obj;
                    using (var outgoingMonitor = new OutgoingConnectionMonitor(log, outgoingVerbOptions, cancellationToken))
                    {
                        var task = outgoingMonitor.MonitorAsync(outgoingVerbOptions.Address, outgoingVerbOptions.Port);
                        task.Wait(cancellationToken);
                    }
                }
                else if (command == "tracert")
                {
                    var tracertVerbOptions = (TraceRouteVerbOptions)obj;
                    var localEndPoint = new IPEndPoint(IPAddress.None, 0);
                    var remoteEndPoint = new IPEndPoint(Dns.GetHostEntry(tracertVerbOptions.Address).AddressList[0], 0);

                    foreach(var reply in TraceRoute.GetTraceRoute(tracertVerbOptions.Address, 30, 2500))
                    {
                        var addr = "*";
                        if (reply.Address != null)
                        {
                            var addressBytes = reply.Address?.GetAddressBytes();
                            addr =
                                $"{addressBytes[0]:D3}.{addressBytes[1]:D3}.{addressBytes[2]:D3}.{addressBytes[3]:D3}";
                        }
                        log.InfoFormat($"TRRT: {localEndPoint.ToString("0G")} -> {remoteEndPoint.ToString("0G")} : {reply.Status}, {reply.RoundtripTime}, {addr}");
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }
            }
            catch (TaskCanceledException taskCancelException)
            {
                log.Debug("Task cancelled", taskCancelException);
            }
            catch (OperationCanceledException cancelException)
            {
                log.Debug("Operation cancelled", cancelException);
            }
            catch (Exception e)
            {
                log.Error("Error while runnning main execution", e);
                try
                {
                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
                catch (TaskCanceledException taskCancelException)
                {
                    log.Debug("Cancelling callbacks", taskCancelException);
                }
                catch (OperationCanceledException cancelException)
                {
                    log.Debug("Cancelling process", cancelException);
                }
                catch (Exception cancelException)
                {
                    log.Warn("Error while cancelling callbacks", cancelException);
                }
            }
            finally
            {
                log.Info("Execution completed. Closing application.");
            }
        }

        public class OutgoingConnectionMonitor : IDisposable
        {
            private readonly ILog log;
            private readonly OutgoingVerbOptions options;
            private readonly CancellationToken cancellationToken;

            public OutgoingConnectionMonitor(ILog log, OutgoingVerbOptions options, CancellationToken cancellationToken)
            {
                this.log = log;
                this.options = options;
                this.cancellationToken = cancellationToken;
            }

            public void Dispose()
            {
            }

            public async Task MonitorAsync(string address, int port)
            {
                var localEndPoint = new IPEndPoint(IPAddress.None, 0);                
                var remoteEndPoint = new IPEndPoint(IPAddress.Parse(address), port);                
                PingPongConnection connection = null;
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            log.InfoFormat("CONN: {0} -> {1}: InProgress",
                                localEndPoint.ToString("0G"), remoteEndPoint.ToString("0G"));
                            connection = new PingPongConnection(log, address, port);
                            log.InfoFormat("CONN: {0} -> {1}: Success",
                                connection.LocalEndPoint, connection.RemoteEndPoint);
                        }
                        catch (SocketException exception)
                        {
                            log.WarnFormat("CONN: {0} -> {1}: Error: {2}",
                                localEndPoint.ToString("0G"), remoteEndPoint.ToString("0G"), exception.SocketErrorCode);

                            cancellationToken.WaitHandle.WaitOne(2000);
                            continue;
                        }

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                log.Info("Pinging connection every 2 seconds");
                                await connection.PlayPingPongAsync(cancellationToken);
                            }
                            catch (SocketException exception)
                            {
                                log.WarnFormat("CONN: {0} -> {1}: Error: {2}",
                                    connection.LocalEndPoint, connection.RemoteEndPoint, exception.SocketErrorCode);

                                break;
                            }
                            catch (Exception exception)
                            {
                                log.Debug("Ping pong play has failed.", exception);
                                log.ErrorFormat("PING: {0} -> {1}: Error: {2} - {3}",
                                    connection.LocalEndPoint, connection.RemoteEndPoint, exception.GetType().Name, exception.Message);

                                Thread.CurrentThread.Join(2000);
                            }
                        }
                    }
                }
                finally
                {
                    connection?.Close();
                }
            }
        }

        private static void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e, CancellationTokenSource cancellationTokenSource, ILog log)
        {
            log.Info("Cancelling connections.");
            cancellationTokenSource.Cancel();
            log.Info("Cancelling done.");
            e.Cancel = cancellationTokenSource.IsCancellationRequested;
        }

        private static ILog InitialiseLogging()
        {
            log4net.Config.XmlConfigurator.Configure();
            var log = LogManager.GetLogger("ROOT");

            var executingAssembly = Assembly.GetExecutingAssembly();
            var header = new string('-', 80);
            log.InfoFormat("{0}", header);
            log.InfoFormat("Starting application version {0}", executingAssembly.FullName);
            log.InfoFormat("Environment: {0}", Environment.OSVersion);
            log.InfoFormat("Environment.CommandLine: {0}", Environment.CommandLine);
            log.InfoFormat("Environment.Version: {0}", Environment.Version);
            log.InfoFormat("Environment.WorkingSet: {0}", Environment.WorkingSet);
            log.InfoFormat("SpecialFolder.ApplicationData: {0}",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            log.InfoFormat("SpecialFolder.CommonApplicationData: {0}",
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
            log.InfoFormat("SpecialFolder.LocalApplicationData: {0}",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            log.InfoFormat("Current directory: {0}", Environment.CurrentDirectory);
            log.InfoFormat("{0}", header);

            return log;
        }
    }
}

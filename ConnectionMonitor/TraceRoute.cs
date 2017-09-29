using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionMonitor
{
    public class TraceRoute
    {
        private const string Data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public static IEnumerable<PingReply> GetTraceRoute(string hostNameOrAddress)
        {
            return GetTraceRoute(hostNameOrAddress, 1, 5000);
        }
        public static IEnumerable<PingReply> GetTraceRoute(string hostNameOrAddress, int ttl, int timeout)
        {
            using(var pinger = new Ping())
            {
                var pingerOptions = new PingOptions(1, true);
                byte[] buffer = Encoding.ASCII.GetBytes(Data);

                PingReply reply = pinger.Send(hostNameOrAddress, timeout, buffer, pingerOptions);
                while (reply != null && pingerOptions.Ttl < ttl)
                {
                    if (reply.Status == IPStatus.Success)
                    {
                        yield return reply;
                        yield break;
                    }
                    if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.TimedOut)
                    {
                        //add the currently returned address if an address was found with this TTL
                        if (reply.Status == IPStatus.TtlExpired)
                        {
                            yield return reply;
                        }
                        else
                        {
                            yield return reply;
                        }
                        //recurse to get the next address...
                        // yield return GetTraceRoute(hostNameOrAddress, ttl + 1, timeout);
                        pingerOptions.Ttl = pingerOptions.Ttl + 1;                        
                        reply = pinger.Send(hostNameOrAddress, timeout, buffer, pingerOptions);
                    }
                }
            }
        }
    }

}

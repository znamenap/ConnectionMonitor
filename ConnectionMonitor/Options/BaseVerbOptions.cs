using CommandLine;

namespace ConnectionMonitor.Options
{
    public class BaseVerbOptions
    {
        [Option('a', "ip-address", Required = true, DefaultValue = "0.0.0.0", HelpText = "IP address where to connect to or where to listen to.")]
        public string Address { get; set; }

        [Option('p', "ip-port", Required = false, DefaultValue = 3859, HelpText = "Port number where to connect to or where to listen to.")]
        public int Port { get; set; }
    }
}
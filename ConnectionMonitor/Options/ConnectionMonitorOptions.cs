using System;
using CommandLine;
using CommandLine.Text;

namespace ConnectionMonitor.Options
{
    public class ConnectionMonitorOptions
    {
        [VerbOption("incomming", HelpText = "Indicates to monitor incomming connection.")]
        public IncommingVerbOptions IncommingVerb { get; set; }

        [VerbOption("outgoing", HelpText = "Indicates to monitor outgoing connection.")]
        public OutgoingVerbOptions OutgoingVerb { get; set; }

        [VerbOption("tracert", HelpText = "Starts trace routing to specified address.")]
        public TraceRouteVerbOptions TraceRouteVerb { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }


        [HelpVerbOption]
        public string GetUsage(string verb)
        {
            try
            {
                return HelpText.AutoBuild(this, verb);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        //[HelpOption]
        //public string GetUsage()
        //{
        //    try
        //    {
        //        return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        throw;
        //    }
        //}
    }
}
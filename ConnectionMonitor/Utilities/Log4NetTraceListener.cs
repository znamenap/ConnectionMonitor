using System.Diagnostics;
using System.Text;
using log4net;

namespace ConnectionMonitor.Utilities
{
    public class Log4NetTraceListener : TraceListener
    {
        private readonly ILog log;
        private readonly StringBuilder builder;

        public Log4NetTraceListener(string logName)
        {
            log = LogManager.GetLogger(logName);
            builder = new StringBuilder();
        }

        /// <summary>
        /// When overridden in a derived class, writes the specified message to the listener you create in the derived class.
        /// </summary>
        /// <param name="message">A message to write. </param><filterpriority>2</filterpriority>
        public override void Write(string message)
        {
            builder.Append(message);
        }

        public override void WriteLine(string message)
        {
            builder.Append(message);
            log.Info(builder.ToString());
            builder.Clear();
        }
    }
}

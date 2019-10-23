using System;
using Datadog.Trace;
using log4net;
using log4net.Config;

namespace Samples.CorrelationIdentifierInjection.Log4Net
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            using (var scope = Tracer.Instance.StartActive("Main()"))
            {
                log.Info("Here's a message");
            }
        }
    }
}

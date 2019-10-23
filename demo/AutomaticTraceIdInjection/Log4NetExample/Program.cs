using System;
using System.IO;
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
            if (args.Length < 1)
            {
                throw new ArgumentException("Pass the config file name as the first argument");
            }

            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            XmlConfigurator.Configure(logRepository, new FileInfo(args[0]));
            
            using (var scope = Tracer.Instance.StartActive("Main()"))
            {
                log.Info("Here's a message");
            }
        }
    }
}

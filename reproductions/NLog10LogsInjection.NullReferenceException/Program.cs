using Datadog.Trace;
using NLog;
using System;
using System.Threading.Tasks;

namespace NLog10LogsInjection.NullReferenceException
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static int Main(string[] args)
        {
            using (var scope = Tracer.Instance.StartActive("Main"))
            {
                Logger.Info("Message during a trace.");

                using (var innerScope = Tracer.Instance.StartActive("Main-Inner"))
                {
                    Logger.Info("Inner message during a trace.");
                }
            }

            Console.WriteLine("Successfully created a trace with two spans and didn't crash. Delay for five seconds to flush the trace.");
            Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            return 0;
        }
    }
}

using Datadog.Trace;
using NLog;

namespace NLog40Example
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            MappedDiagnosticsContext.Set("order-number", 1024.ToString());
            Logger.Info("Message before a trace.");

            using (var scope = Tracer.Instance.StartActive("NLog40Example - Main()"))
            {
                Logger.Info("Message during a trace.");
            }

            Logger.Info("Message after a trace.");
            MappedDiagnosticsContext.Remove("order-number");
        }
    }
}

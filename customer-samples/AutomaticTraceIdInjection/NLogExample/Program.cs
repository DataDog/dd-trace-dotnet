using Datadog.Trace;
using NLog;

namespace NLogExample
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            using (MappedDiagnosticsLogicalContext.SetScoped("order-number", 1024))
            {
                using (var scope = Tracer.Instance.StartActive("NLogExample - Main()"))
                {
                    Logger.Info("Message inside a trace.");
                }

                Logger.Info("Message outside a trace.");
            }
        }
    }
}

using Datadog.Trace;
using NLog;

namespace NLog10LogsInjection.NullReferenceException
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            using (var scope = Tracer.Instance.StartActive("NLog10LogsInjection.NullReferenceException.Program - Main()"))
            {
                Logger.Info("Message during a trace.");
            }
        }
    }
}
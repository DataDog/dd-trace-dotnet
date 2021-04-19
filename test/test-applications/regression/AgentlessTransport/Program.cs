using System;
using System.Threading;
using Datadog.Trace;

namespace AgentlessTransport
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var iterations = 30;
                while (iterations-- > 0)
                {
                    EmitCustomSpans();
                    Console.WriteLine("Trace complete");
                    Thread.Sleep(100);
                }
                return (int)ExitCode.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }
        }

        private static void EmitCustomSpans()
        {
            using (Tracer.Instance.StartActive("custom-span"))
            {
                Thread.Sleep(1500);

                using (Tracer.Instance.StartActive("inner-span"))
                {
                    Thread.Sleep(1500);
                }
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}

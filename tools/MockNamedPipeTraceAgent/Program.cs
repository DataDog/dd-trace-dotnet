using System;
using System.Threading;
using Datadog.Trace.TestHelpers.NamedPipes.Server;

namespace MockNamedPipeTraceAgent
{
    public static class Program
    {
        private static CancellationTokenSource _cancellationTokenSource;
        private static Thread _mainServerThread;
        private static AggregatePipeServer _mainServer;

        public static int Main(string[] args)
        {
            try
            {
                // CustomEnvironmentVariables["DD_TRACE_TRANSPORT"] = "DATADOG-NAMED-PIPES";
                // CustomEnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"] = TracePipeName;
                // CustomEnvironmentVariables["DD_TRACE_PIPE_NAME"] = TracePipeName;
                _cancellationTokenSource = new CancellationTokenSource();
                var pipeName = Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME");

                _mainServerThread = new Thread(
                    () =>
                    {
                        Console.WriteLine("Starting aggregate server.");
                        _mainServer = new AggregatePipeServer(pipeName);
                        _mainServer.Start();

                        while (true)
                        {
                            Thread.Sleep(100);

                            if (_cancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }
                        }
                    });

                Console.WriteLine("Starting server thread.");
                _mainServerThread.Start();

                while (true)
                {
                    Thread.Sleep(100);
                    Console.Write(".");
                    if (_cancellationTokenSource.IsCancellationRequested)
                    {
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return (int)-10;
            }
            finally
            {
                _mainServer.Stop();
                _mainServer.Dispose();
                _cancellationTokenSource.Cancel();
                Thread.Sleep(250);
            }
        }
    }
}

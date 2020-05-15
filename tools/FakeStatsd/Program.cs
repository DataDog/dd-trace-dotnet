using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Serilog;

namespace FakeStatsd
{
    public class Program
    {
        private static readonly int NumThreads = 5;
        private static readonly string LogLocation = "D:\\home\\LogFiles\\datadog\\v0_3_1\\fakestatsd.txt";
        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static readonly bool InAzure = Environment.GetEnvironmentVariable("DD_AZURE_APP_SERVICES") != null;
        private static readonly string PipeName = "testing_dd"; // Environment.GetEnvironmentVariable("DD_FAKE_STATS_PIPE");
        private static ILogger _logger = null;

        public static int Main(string[] args)
        {
            if (PipeName == null)
            {
                throw new ArgumentNullException(nameof(PipeName));
            }

            var loggerConfiguration =
                new LoggerConfiguration()
                   .Enrich.FromLogContext()
                   .WriteTo.File(
                        LogLocation,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}",
                        rollOnFileSizeLimit: true);

            _logger = loggerConfiguration.CreateLogger();

            Log(LogLocation);

            try
            {
                while (CancellationTokenSource.Token.IsCancellationRequested == false)
                {
                    int i;
                    var servers = new Thread[NumThreads];

                    Log("Waiting for client connect...");
                    for (i = 0; i < NumThreads; i++)
                    {
                        servers[i] = new Thread(ServerThread);
                        servers[i].Start();
                    }

                    Thread.Sleep(250);
                    while (i > 0)
                    {
                        for (int j = 0; j < NumThreads; j++)
                        {
                            if (servers[j] != null)
                            {
                                if (servers[j].Join(250))
                                {
                                    Console.WriteLine("Server thread[{0}] finished.", servers[j].ManagedThreadId);
                                    servers[j] = null;
                                    i--;    // decrement the thread watch count
                                }
                            }
                        }
                    }

                    Log("\nServer threads exhausted, exiting.");
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return -1;
            }

            return 0;
        }

        private static void ServerThread(object data)
        {
            var pipeServer =
                new NamedPipeServerStream(PipeName, PipeDirection.InOut, NumThreads);

            var threadId = Thread.CurrentThread.ManagedThreadId;

            // Wait for a client to connect
            pipeServer.WaitForConnection();

            Console.WriteLine("Client connected on thread[{0}].", threadId);
            try
            {
                var ss = new StreamString(pipeServer);
                var fromClient = ss.ReadString();
                Log(fromClient);
            }
            catch (IOException e)
            {
                Log("ERROR: {0}", e.Message);
            }

            pipeServer.Close();
        }

        private static void Log(string line, params object[] interps)
        {
            var formatted = string.Format(line, interps);
            Console.WriteLine(formatted);

            if (!InAzure)
            {
                return;
            }

            _logger.Information(line, interps);
        }
    }
}

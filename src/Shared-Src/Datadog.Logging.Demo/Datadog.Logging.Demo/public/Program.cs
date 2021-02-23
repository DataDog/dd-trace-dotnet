using System;
using System.Threading.Tasks;

namespace Datadog.Logging.Demo
{
    class Program
    {
        private const string LogComponentMoniker = "DemoLogEmitter";

        private const int RuntimeSecs = 20;

        private enum DemoLogAction
        {
            ErrorWithMessage = 1,
            ErrorWithException = 2,
            ErrorWithMessageAndException = 3,
            Debug = 4,
            Info = 5
        }

        private static readonly Random s_rnd = new Random();

        public static void Main(string[] args)
        {
            (new Program()).Run();
        }

        public void Run()
        {
            Console.WriteLine();
            Console.WriteLine($"Console-Message: {typeof(Program).FullName} started.");

            LogConfigurator.SetupLogger();

            Console.WriteLine($"Console-Message: Logger was configured. Starting workers...");

            Task worker1 = Task.Run(() => DoWorkAsync(1));
            Task worker2 = Task.Run(() => DoWorkAsync(2));
            Task worker3 = Task.Run(() => DoWorkAsync(3));

            Console.WriteLine($"Console-Message: Workers started. Running tasks...");

            Task.WaitAll(worker1, worker2, worker3);

            Console.WriteLine($"Console-Message: Workers completed. Disposing log sink(s).");

            LogConfigurator.DisposeLogSink();
            Console.WriteLine($"Console-Message: Log sink(s) disposed. Press Enter to end program.");

            Console.ReadLine();
            Console.WriteLine($"Console-Message: Good bye.");
        }

        private static async Task DoWorkAsync(int workerIdNum)
        {
            DateTimeOffset startTime = DateTimeOffset.Now;

            int iteration = 0;
            string workerId = workerIdNum.ToString("00");

            TimeSpan runtime = DateTimeOffset.Now - startTime;
            while (runtime < TimeSpan.FromSeconds(RuntimeSecs))
            {
                DemoLogAction demoLogAction;
                Exception error = null;

                if (iteration > 0 && iteration % 10 == 0)
                {
                    try
                    {
                        ThrowException();
                        demoLogAction = (DemoLogAction) 0;  // Line never reached
                    }
                    catch(Exception ex)
                    {
                        error = ex;
                        if ((iteration / 10) % 3 == 1)
                        {
                            demoLogAction = DemoLogAction.ErrorWithMessage;
                        }
                        else if ((iteration / 10) % 3 == 2)
                        {
                            demoLogAction = DemoLogAction.ErrorWithException;
                        }
                        else
                        {
                            demoLogAction = DemoLogAction.ErrorWithMessageAndException;
                        }
                    }
                }
                else if (iteration > 0 && iteration % 4 == 0)
                {
                    demoLogAction = DemoLogAction.Debug;
                }
                else
                {
                    demoLogAction = DemoLogAction.Info;
                }

                switch(demoLogAction)
                {
                    case DemoLogAction.ErrorWithMessage:
                        Log.Error(LogComponentMoniker, "An error has occurred", "workerId", workerId, "iteration", iteration, "runtime", runtime, "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithException:
                        Log.Error(LogComponentMoniker, error, "workerId", workerId, "iteration", iteration, "runtime", runtime, "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithMessageAndException:
                        Log.Error(LogComponentMoniker, "An error has occurred", error, "workerId", workerId, "iteration", iteration, "runtime", runtime, "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.Debug:
                        Log.Debug(LogComponentMoniker, "A debug-relevant event occurred", "workerId", workerId, "iteration", iteration, "runtime", runtime, "demoLogAction", demoLogAction);
                        break;

                    default:
                        Log.Info(LogComponentMoniker, "A log-worthy event occurred", "workerId", workerId, "iteration", iteration, "runtime", runtime, "demoLogAction", demoLogAction);
                        break;
                }
                

                if (iteration % 500 == 0)
                {
                    Console.WriteLine($"{Environment.NewLine}Console-Message: Worker {workerId} has been running for {runtime} and completed {iteration} iterations.");
                }

                int delaymillis;
                lock (s_rnd)
                {
                    delaymillis = s_rnd.Next(10);
                }

                await Task.Delay(delaymillis);

                iteration++;
                runtime = DateTimeOffset.Now - startTime;
            }
        }

        private static void ThrowException()
        {
            throw new Exception("An exceptional condition has occurred.");
        }
    }
}

using System;
using System.Threading.Tasks;

namespace Datadog.Logging.Demo.EmitterLib
{
    public class Program
    {
        private const string LogSourceMoniker = "Demo.EmitterLib";

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

        public static void Main(string[] _)
        {
            (new Program()).Run();
        }

        public void Run()
        {
            ConsoleWriteLine();
            ConsoleWriteLine($"{typeof(Program).FullName} started.");

            ConsoleWriteLine($"Starting workers...");

            const int WorkerCount = 4;

            Task[] workers = new Task[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                int workerId = i + 1;
                workers[i] = Task.Run(() => DoWorkAsync(workerId));
            }

            ConsoleWriteLine($"Workers started. Running tasks...");

            Task.WaitAll(workers);
            workers = null;

            ConsoleWriteLine($"Workers completed. Exiting.");

            ConsoleWriteLine($"Good bye.");
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
                    catch (Exception ex)
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

                switch (demoLogAction)
                {
                    case DemoLogAction.ErrorWithMessage:
                        Log.Error(Log.WithCallInfo(LogSourceMoniker),
                                  "A foo-bar error has occurred",
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithException:
                        Log.Error(Log.WithCallInfo(LogSourceMoniker),
                                  error,
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithMessageAndException:
                        Log.Error(Log.WithCallInfo(LogSourceMoniker),
                                  "An foo-bar error has occurred",
                                  error,
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.Debug:
                        Log.Debug(Log.WithCallInfo(LogSourceMoniker).WithSrcFileInfo(),
                                  "A debug-relevant foo-bar event occurred",
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction,
                                  "<UnpairedTag />");
                        break;

                    case DemoLogAction.Info:
                    default:
                        Log.Info(LogSourceMoniker,
                                 "A log-worthy foo-bar event occurred",
                                 "workerId", workerId,
                                 "iteration", iteration,
                                 "runtime", runtime,
                                 "demoLogAction", demoLogAction);
                        break;
                }


                if (iteration % 500 == 0)
                {
                    ConsoleWriteLine();
                    ConsoleWriteLine($"Worker {workerId} has been running for {runtime} and completed {iteration} iterations.");
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
            throw new Exception("An exceptional foo-bar condition has occurred.");
        }

        private static void ConsoleWriteLine()
        {
            ConsoleWriteLine(null);
        }

        private static void ConsoleWriteLine(string line)
        {
            if (line == null)
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("  # # #EmitterLib says: " + line);
            }
        }
    }
}

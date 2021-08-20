using System;
using System.Threading.Tasks;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Demo.EmitterAndComposerApp
{
    public class Program
    {
#pragma warning disable IDE1006  // Runtime-initialized Constants {
        private static readonly LogSourceInfo LogSourceInfo = new LogSourceInfo("Demo.EmitterAndComposerApp");
#pragma warning restore IDE1006  // } Runtime-initialized Constants

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
            (new Datadog.Logging.Demo.EmitterAndComposerApp.Program()).Run();
        }

        public void Run()
        {
            ConsoleWriteLine();
            ConsoleWriteLine($"{typeof(Program).FullName} started.");

            LogConfigurator.SetupLogger();

            ConsoleWriteLine($"Logger was configured.");

            ConsoleWriteLine($"Starting EmitterApp to run in parallel...");

            Task emitterApp = Task.Run((new Datadog.Logging.Demo.EmitterLib.Program()).Run);

            ConsoleWriteLine($"Starting workers...");

            const int WorkerCount = 4;

            Task[] workers = new Task[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                int workerId = i + 1;
                workers[i] = Task.Run(() => DoWorkAsync(workerId));
            }

            ConsoleWriteLine($"Workers started. Running tasks...");

            Task allWorkersTask = Task.WhenAll(workers);
            workers = null;

            Task.WaitAll(emitterApp, allWorkersTask);

            ConsoleWriteLine($"Workers and the EmitterApp completed. Disposing log sink(s).");

            LogConfigurator.DisposeLogSinks();
            ConsoleWriteLine($"Log sink(s) disposed. Press Enter to end program.");

            Console.ReadLine();
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
                        Log.Error(LogSourceInfo.WithCallInfo().WithinLogSourcesGroup("Some Large Component"),
                                  "An error has occurred",
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithException:
                        Log.Error(LogSourceInfo.WithCallInfo().WithLogSourcesSubgroup("Some Subcomponent"),
                                  error,
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.ErrorWithMessageAndException:
                        Log.Error(LogSourceInfo.WithCallInfo(),
                                  "An error has occurred",
                                  error,
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction);
                        break;

                    case DemoLogAction.Debug:
                        Log.Debug(LogSourceInfo.WithSrcFileInfo(),
                                  "A debug-relevant event occurred",
                                  "workerId", workerId,
                                  "iteration", iteration,
                                  "runtime", runtime,
                                  "demoLogAction", demoLogAction,
                                  "<UnpairedTag />");
                        break;

                    case DemoLogAction.Info:
                    default:
                        Log.Info(LogSourceInfo,
                                 "A log-worthy event occurred",
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
            throw new Exception("An exceptional condition has occurred.");
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
                Console.WriteLine(" # # # EmitterAndComposerApp.Program says: " + line);
            }
        }
    }
}

using System;
using System.Threading.Tasks;

namespace Datadog.Logging.Demo
{
    class Program
    {
        private const string LogComponentMoniker = "LoggingDemo";

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

            Console.WriteLine($"Console-Message: Workers completed. Press Enter to end program.");

            Console.ReadLine();
            Console.WriteLine($"Console-Message: Good bye.");
        }

        private static async Task DoWorkAsync(int workerIdNum)
        {
            const int RuntimeSecs = 120;

            DateTimeOffset startTime = DateTimeOffset.Now;

            int iteration = 0;
            string workerId = workerIdNum.ToString("00");

            TimeSpan runtime = DateTimeOffset.Now - startTime;
            while (runtime < TimeSpan.FromSeconds(RuntimeSecs))
            {
                Log.Info(LogComponentMoniker, "A log-worthy event has happened", "workerId", workerId, "iteration", iteration, "runtime", runtime);

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
    }
}

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Website_AspNet
{
    public class SelfInvoker : IDisposable
    {
        private const int DefaultInput = 42;
        private const int DefaultParalellism = 1;

        private readonly string PullUrl;
        private readonly int _fibonacciInput;
        private readonly Thread[] _workers;

        private readonly HttpClient _httpClient;
        private volatile bool _isRunning;

        public SelfInvoker()
        {
            _httpClient = new HttpClient();
            _workers = CreateWorkers(EnvironmentHelper.GetParallelism(DefaultParalellism));
            _fibonacciInput = EnvironmentHelper.GetFibonacciInput(DefaultInput);
            PullUrl = $"http://localhost:80/?number={_fibonacciInput.ToString()}";
            _isRunning = false;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public void Start()
        {
            _isRunning = true;
            foreach (var worker in _workers)
            {
                worker.Start();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            foreach (var worker in _workers)
            {
                worker.Join();
            }
        }

        private Thread[] CreateWorkers(int parallelism)
        {
            var workers = new Thread[parallelism];

            for (var i = 0; i < parallelism; i++)
            {
                workers[i] = new Thread(WorkerLoop);
            }

            return workers;
        }

        private void WorkerLoop()
        {
            Console.WriteLine($"{this.GetType().Name} started.");

            var rnd = new Random();
            while (_isRunning)
            {
                var sleepDuration = TimeSpan.FromSeconds(rnd.Next(1, 6));
                Thread.Sleep(sleepDuration);
                SendRequest();
            }

            Console.WriteLine($"{this.GetType().Name} stopped.");
        }

        private void SendRequest()
        {
            try
            {
                var response = _httpClient.GetAsync(PullUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Error] Request failed {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine($"Request succeeded");
                }

                string responsePayload = response.Content.ReadAsStringAsync().Result;
                int responseLen = responsePayload.Length;
                Console.WriteLine($"Received response length: {responseLen}");
            }
            catch (TaskCanceledException tce)
            {
                Console.WriteLine($"SelfInvokerService: Current request cancelled ({tce}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex}");
            }
        }
    }
}

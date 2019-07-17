using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace AspNetMvcCorePerformance
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var test = new HttpClient();
                var testU = "http://localhost:54562/delay/0";
                var task = test.GetAsync(testU);
                task.Wait();
                var res = task.Result;

                var totalIterations = 200;
                var threadRepresentation = Enumerable.Range(0, 10).ToArray();
                var threadCount = threadRepresentation.Length;

                var exceptionBag = new ConcurrentBag<Exception>();

                Console.WriteLine($"Running {threadRepresentation.Length} threads with {totalIterations} iterations.");

                var resources = new List<string>()
                {
                    "delay/0", // 1
                    "delay-async/0",
                    "delay-async/0",
                    "home/index",
                    "home/index",
                    //"bad-request",
                    "home/index",
                    "status-code/200",
                    "delay/0",
                    "delay/1",
                    "home/index" // 10
                };

                var threadNumber = 0;

                var threads =
                    threadRepresentation
                       .Select(
                            idx => new Thread(
                                thread =>
                                {
                                    try
                                    {
                                        var myThread = threadNumber++;
                                        var myResource = resources[myThread];

                                        Thread.Sleep(2000);
                                        var i = 0;
                                        while (i++ < totalIterations)
                                        {
                                            Console.WriteLine($"(Thread: {myThread}, #: {i}) Calling: {myResource}");
                                            var client = new HttpClient();
                                            var uri = $"http://localhost:54562/{myResource}";
                                            var responseTask = client.GetAsync(uri);
                                            responseTask.Wait();
                                            var result = responseTask.Result;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptionBag.Add(ex);
                                    }
                                }))
                       .ToList();

                foreach (var thread in threads)
                {
                    thread.Start();
                }

                while (threads.Any(x => x.IsAlive))
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return -10;
            }

            return 0;
        }
    }
}

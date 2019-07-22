using System;
using System.Net.Http;
using System.Threading;

namespace AppDomain.Instance
{
    public class Program
    {
        public int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting AppDomain Instance Test");

                string appDomainName = "crash-dummy";
                int index = 1;

                if (args?.Length > 0)
                {
                    appDomainName = args[0];
                    index = int.Parse(args[1]);
                }

                var instance = new NestedProgram()
                {
                    AppDomainName = appDomainName,
                    AppDomainIndex = index
                };

                // Act like we're doing some continuing work
                while (true)
                {
                    Thread.Sleep(500);
                    instance.MakeSomeCall();

                    if (instance.CallCount > 10)
                    {
                        // Meh, call it quits
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception in this instance: {ex.Message}");
                Console.Error.WriteLine(ex);
                return -10;
            }

            return 0;
        }

        public class NestedProgram
        {
            public string AppDomainName { get; set; }
            public int AppDomainIndex { get; set; }
            public int CallCount { get; set; }

            public void MakeSomeCall()
            {
                try
                {
                    var baseAddress = new Uri("https://www.example.com/");
                    var regularHttpClient = new HttpClient { BaseAddress = baseAddress };
                    Console.WriteLine($"App {AppDomainIndex} - Starting client.GetAsync");
                    regularHttpClient.GetAsync("default-handler").Wait();
                    Console.WriteLine($"App {AppDomainIndex} - Finished client.GetAsync");
                }
                finally
                {
                    CallCount++;
                }
            }
        }
    }
}

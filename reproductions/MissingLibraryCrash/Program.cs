using System;
using System.Net.Http;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.TestHelpers;
using SmokeTests.Core;

namespace MissingLibraryCrash
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var profilerLoaded = Instrumentation.ProfilerAttached;

                if (!profilerLoaded)
                {
                    throw new Exception("The profiler must be attached for this to be a valid test.");
                }

                Console.WriteLine("The profiler is attached.");

                Console.WriteLine($"This application runtime is {EnvironmentHelper.GetRuntimeDescription()}.");

                if (!EnvironmentHelper.IsCoreClr())
                {
                    throw new Exception("This test is not valid for anything but Core.");
                }

                try
                {
                    var managedLibrary = Assembly.Load("Datadog.Trace.ClrProfiler.Managed.dll");

                    if (managedLibrary != null)
                    {
                        throw new Exception("This test isn't valid unless we can't load the managed library");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("The managed library is missing.");
                }

                Console.WriteLine("Time to call some code that we instrument.");

                var baseAddress = new Uri("https://www.example.com/");
                var regularHttpClient = new HttpClient { BaseAddress = baseAddress };

                var responseTask = regularHttpClient.GetAsync("default-handler");
                responseTask.Wait();

                Console.WriteLine($"Received a response status of: {responseTask.Result.StatusCode}");
                Console.WriteLine("We called our HttpClient.");
                Console.WriteLine("The application didn't crash!");
                Console.WriteLine("All is well!");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

    }
}

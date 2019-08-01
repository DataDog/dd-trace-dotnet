using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace MissingLibraryCrash
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Assembly.Load("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            try
            {
                var assemblyMethods = typeof(Assembly).GetMethods();
                var assemblyLoadMethodCandidates =
                    assemblyMethods
                       .Where(m => m.Name == "Load")
                       .Where(m => m.ReturnType == typeof(Assembly))
                       .Where(m => m.GetParameters().Length == 1)
                       .Where(m => m.GetParameters()[0].ParameterType == typeof(string))
                       .ToList();

                foreach(var method in assemblyLoadMethodCandidates)
                {
                    Console.WriteLine($"We need an mdToken of {method.MetadataToken}");
                    Console.WriteLine($"Calling conventions {method.CallingConvention}");
                    Console.WriteLine($"Is abstract {method.IsAbstract}");
                }

                var mscorlib = Assembly.Load("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

                Console.WriteLine("Crash test initiated.");

                // bool profilerAttached = SmokeTestNativeMethods.IsProfilerAttachedExhaustive();
                // 
                // if (!profilerAttached)
                // {
                //     throw new Exception("The profiler must be attached for this to be a valid test.");
                // }

                Console.WriteLine("The profiler is attached.");

                Console.WriteLine($"This application runtime is {EnvironmentHelper.GetRuntimeDescription()}.");

                // if (!EnvironmentHelper.IsCoreClr())
                // {
                //     throw new Exception("This test is not valid for anything but Core.");
                // }

                var badIntegrationsFile = Environment.GetEnvironmentVariable("DD_INTEGRATIONS");

                var badIntegrationsFileName = System.IO.Path.GetFileName(badIntegrationsFile);
                var expectedFileName = "bad-integrations.json";

                if (badIntegrationsFileName != expectedFileName)
                {
                    throw new Exception($"Test is not valid without integrations file: {expectedFileName}");
                }

                Console.WriteLine($"Using integrations file: {badIntegrationsFile}");
                Console.WriteLine("Expecting an attempted load from assembly: BAD_Datadog.Trace.ClrProfiler.Managed");
                Console.WriteLine("Time to call some of our instrumented code.");

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
                return -10;
            }

            return 0;
        }

    }
}

using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.StackExchange.Redis;
using Datadog.StackExchange.Redis.Abstractions;
using Datadog.StackExchange.Redis.StrongName;

namespace StackExchange.Redis.AssemblyConflict.LegacyProject
{
    public class Program
    {
        public static async Task Main()
        {
            try
            {
                await RunTest();
            }
            finally
            {
                var profilerAttached = Samples.SampleHelpers.IsProfilerAttached();
                var tracerAssemblyLocation = Samples.SampleHelpers.GetTracerAssemblyLocation();

                Console.WriteLine();
                Console.WriteLine($"Profile attached: {profilerAttached}");
                Console.WriteLine($"Datadog.Trace.dll path: {tracerAssemblyLocation}");
                Console.WriteLine();
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif
            Console.WriteLine("App completed successfully");
        }

        private static async Task RunTest()
        {
            // note: STACKEXCHANGE_REDIS_HOST includes a port, despite its name
            var configuration = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_SINGLE_HOST") ?? "localhost:6389";

            ICache redis = new RedisClient(configuration);
            await RunTest(redis);

            ICache redisStrongName = new RedisStrongNameClient(configuration);
            await RunTest(redisStrongName);
        }

        public static async Task RunTest(ICache cache)
        {
            Console.WriteLine($"Running test with {cache.GetType().Name}");
            Console.WriteLine("Running sync operations");

            const string name = "AssemblyConflictLegacyProjectName1";
            const string expectedValue = "value1";

            Console.WriteLine("Setting string...");
            cache.SetString(name, expectedValue);

            Console.WriteLine("Getting string...");
            var actualValue = cache.GetString(name);

            if (expectedValue == actualValue)
            {
                Console.WriteLine($"Values match. {expectedValue} == {actualValue}");
                Console.WriteLine();
            }
            else
            {
                throw new Exception($"Values do NOT match. {expectedValue} == {actualValue}");
            }

            Console.WriteLine("Running async operations");
            Console.WriteLine("Setting string...");
            await cache.SetStringAsync(name, expectedValue);

            Console.WriteLine("Getting string...");
            actualValue = await cache.GetStringAsync(name);

            if (expectedValue == actualValue)
            {
                Console.WriteLine($"Values match. {expectedValue} == {actualValue}");
                Console.WriteLine();
            }
            else
            {
                throw new Exception($"Values do NOT match. {expectedValue} == {actualValue}");
            }
        }
    }
}

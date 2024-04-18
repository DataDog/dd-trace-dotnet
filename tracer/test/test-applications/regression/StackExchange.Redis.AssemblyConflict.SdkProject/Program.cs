using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.StackExchange.Redis;
using Datadog.StackExchange.Redis.Abstractions;
using Datadog.StackExchange.Redis.StrongName;

namespace StackExchange.Redis.AssemblyConflict.SdkProject
{
    public class Program
    {
        public static async Task<int> Main()
        {
            try
            {
                await RunTest();
            }
            catch (Exception ex)
                when (ex.GetType().Name == "RedisConnectionException"
                   && (ex.Message.Contains("No connection is available to service this operation")
                      || ex.Message.Contains("It was not possible to connect to the redis server")))
            {
                // If the redis server is being too slow in responding, we can end up with timeouts
                // We could do retries, but then we risk butting up against timeout limits etc
                // As a workaround, we use the specific exit code 13 to indicate a faulty program,
                // and skip the test.
                // We need to keep the catch very specific here, so that we don't accidentally
                // start skipping tests when we shouldn't be
                Console.WriteLine("Unexpected exception during execution " + ex);
                Console.WriteLine("Exiting with skip code (13)");
                return 13;
                
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
            return 0;
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

            const string name = "AssemblyConflictSdkProjectName1";
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

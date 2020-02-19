using System;
using System.Reflection;
using Datadog.StackExchange.Redis;
using Datadog.StackExchange.Redis.Abstractions;
using Datadog.StackExchange.Redis.StrongName;

namespace StackExchange.Redis.AssemblyConflict.SdkProject
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                RunTest();
            }
            finally
            {
                var instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace.ClrProfiler.Managed");
                var profilerAttached = instrumentationType?.GetProperty("ProfilerAttached", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? false;
                var tracerAssemblyLocation = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace")?.Assembly.Location ?? "(none)";
                var clrProfilerAssemblyLocation = instrumentationType?.Assembly.Location ?? "(none)";
                var sigilAssemblyLocation = Type.GetType("Sigil.Local, Sigil")?.Assembly.Location ?? "(none)";

                Console.WriteLine();
                Console.WriteLine($"Profile attached: {profilerAttached}");
                Console.WriteLine($"Datadog.Trace.dll path: {tracerAssemblyLocation}");
                Console.WriteLine($"Datadog.Trace.ClrProfiler.Managed.dll path: {clrProfilerAssemblyLocation}");
                Console.WriteLine($"Sigil.dll path: {sigilAssemblyLocation}");
                Console.WriteLine();
            }
        }

        private static void RunTest()
        {
            // note: STACKEXCHANGE_REDIS_HOST includes a port, despite its name
            var configuration = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";

            ICache redis = new RedisClient(configuration);
            RunTest(redis);

            ICache redisStrongName = new RedisStrongNameClient(configuration);
            RunTest(redisStrongName);
        }

        public static void RunTest(ICache cache)
        {
            Console.WriteLine($"Running test with {cache.GetType().Name}");

            const string name = "name1";
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

        }
    }
}

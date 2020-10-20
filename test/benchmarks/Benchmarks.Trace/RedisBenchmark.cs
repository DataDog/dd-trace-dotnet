using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using ServiceStack.Redis;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class RedisBenchmark
    {
        private static readonly int MdToken;
        private static readonly IntPtr GuidPtr;
        private static readonly object Client = new RedisNativeClient();
        private static readonly Func<int> Fn = () => 42;
        private static readonly Action<Func<int>> CompletePipelineFn = _ => { };
        private static readonly byte[][] RawCommands;

        static RedisBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var methodInfo = typeof(RedisNativeClient).GetMethod("SendReceive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            MdToken = methodInfo.MetadataToken;
            var guid = typeof(RedisNativeClient).Module.ModuleVersionId;

            GuidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, GuidPtr, false);

            RawCommands = new[] {"Command", "arg1", "arg2"}
                .Select(Encoding.UTF8.GetBytes)
                .ToArray();

            // new RedisBenchmark().ExecuteNonQuery();
        }

        [Benchmark]
        public int SendReceive()
        {
            return ServiceStackRedisIntegration.SendReceive<int>(
                Client,
                RawCommands,
                Fn,
                CompletePipelineFn,
                true,
                (int)OpCodeValue.Callvirt,
                MdToken,
                (long)GuidPtr);
        }
    }
}

namespace ServiceStack.Redis
{
    internal class RedisNativeClient
    {
        public string Host { get; } = "Host";
        public int Port { get; } = 80;

        public T SendReceive<T>(byte[][] cmdWithBinaryArgs, Func<T> fn, Action<Func<T>> completePipelineFn, bool sendWithoutRead)
        {
            return fn();
        }
    }

    internal interface ISomething
    {
        void Method();
    }

    internal struct Test : ISomething
    {
        void ISomething.Method()
        {

        }
    }
}

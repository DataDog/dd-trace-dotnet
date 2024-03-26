using System;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.ServiceStack;
using Datadog.Trace.Configuration;
using ServiceStack.Redis;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent4]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class RedisBenchmark
    {
        private static readonly RedisNativeClient Client = new RedisNativeClient();
        private static readonly Func<int> Fn = () => 42;
        private static readonly Action<Func<int>> CompletePipelineFn = _ => { };
        private static readonly byte[][] RawCommands;

        static RedisBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            RawCommands = new[] {"Command", "arg1", "arg2"}
                .Select(Encoding.UTF8.GetBytes)
                .ToArray();
        }

        [Benchmark]
        public unsafe int SendReceive()
        {
            return CallTarget.Run<RedisNativeClientSendReceiveIntegration, RedisNativeClient, byte[][], Func<int>, Action<Func<int>>, bool, int>
                (Client, RawCommands, Fn, CompletePipelineFn, true, &SendReceive);

            static int SendReceive(byte[][] cmdWithBinaryArgs, Func<int> fn, Action<Func<int>> completePipelineFn, bool sendWithoutRead) => fn();
        }
    }
}

namespace ServiceStack.Redis
{
    internal class RedisNativeClient
    {
        public long Db { get; } = 2;
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

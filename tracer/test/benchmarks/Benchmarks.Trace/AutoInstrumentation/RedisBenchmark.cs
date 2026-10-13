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
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class RedisBenchmark
    {
        private RedisNativeClient _client;
        private Func<int> _fn;
        private Action<Func<int>> _completePipelineFn;
        private byte[][] _rawCommands;

        [GlobalSetup]
        public void GlobalSetup()
        {
            TracerHelper.SetGlobalTracer();
            _client = new RedisNativeClient();
            _fn = () => 42;
            _completePipelineFn = _ => { };
            _rawCommands = new[] {"Command", "arg1", "arg2"}
                .Select(Encoding.UTF8.GetBytes)
                .ToArray();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public unsafe int SendReceive()
        {
            return CallTarget.Run<RedisNativeClientSendReceiveIntegration, RedisNativeClient, byte[][], Func<int>, Action<Func<int>>, bool, int>
                (_client, _rawCommands, _fn, _completePipelineFn, true, &SendReceiveImpl);

            static int SendReceiveImpl(byte[][] cmdWithBinaryArgs, Func<int> fn, Action<Func<int>> completePipelineFn, bool sendWithoutRead) => fn();
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

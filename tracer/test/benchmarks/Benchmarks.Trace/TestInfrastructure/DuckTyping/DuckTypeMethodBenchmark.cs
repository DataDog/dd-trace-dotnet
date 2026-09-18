using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.DuckTyping;

namespace Benchmarks.Trace.DuckTyping
{
    [DatadogExporter]
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class DuckTypeMethodBenchmark
    {
        public static ObscureObject.IBaseMethodRunner BaseObject = (ObscureObject.IBaseMethodRunner)ObscureObject.GetPropertyPublicObject();

        public static IEnumerable<IMethodRunner> Proxies()
        {
            return new IMethodRunner[]
            {
                ObscureObject.GetPropertyPublicObject().As<IMethodRunner>(),
                ObscureObject.GetPropertyInternalObject().As<IMethodRunner>(),
                ObscureObject.GetPropertyPrivateObject().As<IMethodRunner>()
            };
        }

        /**
         * Execute a void return method
         */

        [Benchmark]
        [BenchmarkCategory("Void Method")]
        public void PublicVoidMethod()
        {
            BaseObject.Add("key", "value");
        }

        [Benchmark]
        [BenchmarkCategory("Void Method")]
        [ArgumentsSource(nameof(Proxies))]
        public void PublicVoidMethod(IMethodRunner proxy)
        {
            proxy.Add("key", "value");
        }

        [Benchmark]
        [BenchmarkCategory("Void Method")]
        [ArgumentsSource(nameof(Proxies))]
        public void PrivateVoidMethod(IMethodRunner proxy)
        {
            proxy.AddPrivate("key", "value");
        }

        /**
         * Execute a method with return value
         */

        [Benchmark]
        [BenchmarkCategory("Method")]
        public string PublicMethod()
        {
            return BaseObject.Get("key");
        }

        [Benchmark]
        [BenchmarkCategory("Method")]
        [ArgumentsSource(nameof(Proxies))]
        public string PublicMethod(IMethodRunner proxy)
        {
            return proxy.Get("key");
        }

        [Benchmark]
        [BenchmarkCategory("Method")]
        [ArgumentsSource(nameof(Proxies))]
        public string PrivateMethod(IMethodRunner proxy)
        {
            return proxy.GetPrivate("key");
        }

        /**
         * Execute a method with output parameter
         */

        [Benchmark]
        [BenchmarkCategory("Out-Param Method")]
        public bool PublicOutParameterMethod()
        {
            return BaseObject.TryGetValue("key", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Out-Param Method")]
        [ArgumentsSource(nameof(Proxies))]
        public bool PublicOutParameterMethod(IMethodRunner proxy)
        {
            return proxy.TryGetValue("key", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Out-Param Method")]
        [ArgumentsSource(nameof(Proxies))]
        public bool PrivateOutParameterMethod(IMethodRunner proxy)
        {
            return proxy.TryGetValuePrivate("key", out string value);
        }


        public interface IMethodRunner
        {
            void Add(string key, string value);

            void AddPrivate(string key, string value);

            string Get(string key);

            string GetPrivate(string key);

            bool TryGetValue(string key, out string value);

            bool TryGetValuePrivate(string key, out string value);

            string ToString();
        }
    }
}

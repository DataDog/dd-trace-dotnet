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
    public class DuckTypeValueTypePropertyBenchmark
    {
        public static ObscureObject.IObscureObject BaseObject = (ObscureObject.IObscureObject)ObscureObject.GetPropertyPublicObject();

        public static IEnumerable<IObscureDuckType> Proxies()
        {
            return new IObscureDuckType[]
            {
                ObscureObject.GetPropertyPublicObject().As<IObscureDuckType>(),
                ObscureObject.GetPropertyInternalObject().As<IObscureDuckType>(),
                ObscureObject.GetPropertyPrivateObject().As<IObscureDuckType>()
            };
        }

        /**
         * Get Static Property
         */

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPublicStaticProperty(IObscureDuckType proxy)
        {
            return proxy.PublicStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetInternalStaticProperty(IObscureDuckType proxy)
        {
            return proxy.InternalStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetProtectedStaticProperty(IObscureDuckType proxy)
        {
            return proxy.ProtectedStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPrivateStaticProperty(IObscureDuckType proxy)
        {
            return proxy.PrivateStaticGetSetValueType;
        }


        /**
         * Set Static Property
         */

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPublicStaticProperty(IObscureDuckType proxy)
        {
            proxy.PublicStaticGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetInternalStaticProperty(IObscureDuckType proxy)
        {
            proxy.InternalStaticGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetProtectedStaticProperty(IObscureDuckType proxy)
        {
            proxy.ProtectedStaticGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPrivateStaticProperty(IObscureDuckType proxy)
        {
            proxy.PrivateStaticGetSetValueType = 42;
        }


        /**
         * Get Property
         */

        [Benchmark]
        [BenchmarkCategory("Getter")]
        public int GetPublicProperty()
        {
            return BaseObject.PublicGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPublicProperty(IObscureDuckType proxy)
        {
            return proxy.PublicGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetInternalProperty(IObscureDuckType proxy)
        {
            return proxy.InternalGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetProtectedProperty(IObscureDuckType proxy)
        {
            return proxy.ProtectedGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPrivateProperty(IObscureDuckType proxy)
        {
            return proxy.PrivateGetSetValueType;
        }


        /**
         * Set Property
         */

        [Benchmark]
        [BenchmarkCategory("Setter")]
        public void SetPublicProperty()
        {
            BaseObject.PublicGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPublicProperty(IObscureDuckType proxy)
        {
            proxy.PublicGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetInternalProperty(IObscureDuckType proxy)
        {
            proxy.InternalGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetProtectedProperty(IObscureDuckType proxy)
        {
            proxy.ProtectedGetSetValueType = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPrivateProperty(IObscureDuckType proxy)
        {
            proxy.PrivateGetSetValueType = 42;
        }


        /**
         * Indexer
         */

        [Benchmark]
        [BenchmarkCategory("Indexer Getter")]
        public int GetIndexerProperty()
        {
            return BaseObject[42];
        }

        [Benchmark]
        [BenchmarkCategory("Indexer Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetIndexerProperty(IObscureDuckType proxy)
        {
            return proxy[42];
        }

        [Benchmark]
        [BenchmarkCategory("Indexer Setter")]
        public void SetIndexerProperty()
        {
            BaseObject[42] = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Indexer Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetIndexerProperty(IObscureDuckType proxy)
        {
            proxy[42] = 42;
        }

        public interface IObscureDuckType
        {
            int PublicStaticGetSetValueType { get; set; }

            int InternalStaticGetSetValueType { get; set; }

            int ProtectedStaticGetSetValueType { get; set; }

            int PrivateStaticGetSetValueType { get; set; }

            // *

            int PublicGetSetValueType { get; set; }

            int InternalGetSetValueType { get; set; }

            int ProtectedGetSetValueType { get; set; }

            int PrivateGetSetValueType { get; set; }

            // *

            int this[int index] { get; set; }

            string ToString();
        }
    }
}

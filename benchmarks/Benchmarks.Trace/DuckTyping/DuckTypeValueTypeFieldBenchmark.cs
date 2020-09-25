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
    public class DuckTypeValueTypeFieldBenchmark
    {
        public static IEnumerable<IObscureDuckType> Proxies()
        {
            return new IObscureDuckType[]
            {
                ObscureObject.GetFieldPublicObject().As<IObscureDuckType>(),
                ObscureObject.GetFieldInternalObject().As<IObscureDuckType>(),
                ObscureObject.GetFieldPrivateObject().As<IObscureDuckType>()
            };
        }


        /**
         * Get Static Field
         */

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPublicStaticField(IObscureDuckType proxy)
        {
            return proxy.PublicStaticValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetInternalStaticField(IObscureDuckType proxy)
        {
            return proxy.InternalStaticValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetProtectedStaticField(IObscureDuckType proxy)
        {
            return proxy.ProtectedStaticValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPrivateStaticField(IObscureDuckType proxy)
        {
            return proxy.PrivateStaticValueTypeField;
        }


        /**
         * Set Static Field
         */

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPublicStaticField(IObscureDuckType proxy)
        {
            proxy.PublicStaticValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetInternalStaticField(IObscureDuckType proxy)
        {
            proxy.InternalStaticValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetProtectedStaticField(IObscureDuckType proxy)
        {
            proxy.ProtectedStaticValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Static Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPrivateStaticField(IObscureDuckType proxy)
        {
            proxy.PrivateStaticValueTypeField = 42;
        }


        /**
         * Get Field
         */

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPublicField(IObscureDuckType proxy)
        {
            return proxy.PublicValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetInternalField(IObscureDuckType proxy)
        {
            return proxy.InternalValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetProtectedField(IObscureDuckType proxy)
        {
            return proxy.ProtectedValueTypeField;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Proxies))]
        public int GetPrivateField(IObscureDuckType proxy)
        {
            return proxy.PrivateValueTypeField;
        }


        /**
         * Set Field
         */

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPublicField(IObscureDuckType proxy)
        {
            proxy.PublicValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetInternalField(IObscureDuckType proxy)
        {
            proxy.InternalValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetProtectedField(IObscureDuckType proxy)
        {
            proxy.ProtectedValueTypeField = 42;
        }

        [Benchmark]
        [BenchmarkCategory("Setter")]
        [ArgumentsSource(nameof(Proxies))]
        public void SetPrivateField(IObscureDuckType proxy)
        {
            proxy.PrivateValueTypeField = 42;
        }


        public interface IObscureDuckType
        {
            [Duck(Name = "_publicStaticValueTypeField", Kind = DuckKind.Field)]
            int PublicStaticValueTypeField { get; set; }

            [Duck(Name = "_internalStaticValueTypeField", Kind = DuckKind.Field)]
            int InternalStaticValueTypeField { get; set; }

            [Duck(Name = "_protectedStaticValueTypeField", Kind = DuckKind.Field)]
            int ProtectedStaticValueTypeField { get; set; }

            [Duck(Name = "_privateStaticValueTypeField", Kind = DuckKind.Field)]
            int PrivateStaticValueTypeField { get; set; }

            // *

            [Duck(Name = "_publicValueTypeField", Kind = DuckKind.Field)]
            int PublicValueTypeField { get; set; }

            [Duck(Name = "_internalValueTypeField", Kind = DuckKind.Field)]
            int InternalValueTypeField { get; set; }

            [Duck(Name = "_protectedValueTypeField", Kind = DuckKind.Field)]
            int ProtectedValueTypeField { get; set; }

            [Duck(Name = "_privateValueTypeField", Kind = DuckKind.Field)]
            int PrivateValueTypeField { get; set; }

            string ToString();
        }
    }
}

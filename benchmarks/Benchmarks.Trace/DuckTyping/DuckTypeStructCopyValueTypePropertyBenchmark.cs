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
    public class DuckTypeStructCopyValueTypePropertyBenchmark
    {
        public static ObscureObject.IObscureObject BaseObject = (ObscureObject.IObscureObject)ObscureObject.GetPropertyPublicObject();

        public static IEnumerable<object> Source()
        {
            return new object[]
            {
                ObscureObject.GetPropertyPublicObject(),
                ObscureObject.GetPropertyInternalObject(),
                ObscureObject.GetPropertyPrivateObject()
            };
        }

        static DuckTypeStructCopyValueTypePropertyBenchmark()
        {
            foreach (var item in Source())
            {
                item.As<ObscureDuckTypeStruct>();
            }
        }

        /**
         * Get Static Property
         */

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetPublicStaticProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.PublicStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetInternalStaticProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.InternalStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetProtectedStaticProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.ProtectedStaticGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Static Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetPrivateStaticProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.PrivateStaticGetSetValueType;
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
        [ArgumentsSource(nameof(Source))]
        public int GetPublicProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.PublicGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetInternalProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.InternalGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetProtectedProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.ProtectedGetSetValueType;
        }

        [Benchmark]
        [BenchmarkCategory("Getter")]
        [ArgumentsSource(nameof(Source))]
        public int GetPrivateProperty(object value)
        {
            var proxy = value.As<ObscureDuckTypeStruct>();
            return proxy.PrivateGetSetValueType;
        }

        [DuckCopy]
        public struct ObscureDuckTypeStruct
        {
            public int PublicStaticGetSetValueType;

            public int InternalStaticGetSetValueType;

            public int ProtectedStaticGetSetValueType;

            public int PrivateStaticGetSetValueType;

            // *

            public int PublicGetSetValueType;

            public int InternalGetSetValueType;

            public int ProtectedGetSetValueType;

            public int PrivateGetSetValueType;
        }
    }
}

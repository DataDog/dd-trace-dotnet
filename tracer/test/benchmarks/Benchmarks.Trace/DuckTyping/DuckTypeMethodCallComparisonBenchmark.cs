using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.DuckTyping;
using static Benchmarks.Trace.DuckTyping.ObscureObject;

namespace Benchmarks.Trace.DuckTyping
{
    [DatadogExporter]
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class DuckTypeMethodCallComparisonBenchmark
    {
        internal delegate CallTargetState InvokeDelegate(PropertyPublicObject instance);

        internal static InvokeDelegate _voidMethodIntegrationDelegate;
        internal static InvokeDelegate _returnMethodIntegrationDelegate;
        internal static InvokeDelegate _getPropertiesIntegrationDelegate;

        internal static InvokeDelegate _voidMethodIDuckTypeIntegrationDelegate;
        internal static InvokeDelegate _returnMethodIDuckTypeIntegrationDelegate;
        internal static InvokeDelegate _getPropertiesIDuckTypeIntegrationDelegate;

        public static IEnumerable<object> GetObjects()
        {
            return new object[]
            {
                ObscureObject.GetPropertyPublicObject(),
            };
        }

        static DuckTypeMethodCallComparisonBenchmark()
        {
            foreach (var obj in GetObjects())
            {
                obj.DuckCast<IMethodRunner>();
                obj.DuckCast<MethodRunnerClass>();
                obj.DuckCast<AbstractMethodRunner>();
                obj.DuckCast<ValuesCopy>();
            }

            _voidMethodIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(VoidMethodIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));
            _returnMethodIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(ReturnMethodIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));
            _getPropertiesIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(GetPropertiesIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));

            _voidMethodIDuckTypeIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(VoidMethodIDuckTypeIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));
            _returnMethodIDuckTypeIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(ReturnMethodIDuckTypeIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));
            _getPropertiesIDuckTypeIntegrationDelegate = (InvokeDelegate)IntegrationMapper.CreateBeginMethodDelegate(typeof(GetPropertiesIDuckTypeIntegration), typeof(PropertyPublicObject), Type.EmptyTypes).CreateDelegate(typeof(InvokeDelegate));
        }

        // Void Method

        [Benchmark]
        [BenchmarkCategory("proxy.Add(\"key\", \"value\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public void VoidMethodInterface(object value)
        {
            var proxy = value.DuckCast<IMethodRunner>();
            proxy.Add("key", "value");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Add(\"key\", \"value\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public void VoidMethodClass(object value)
        {
            var proxy = value.DuckCast<MethodRunnerClass>();
            proxy.Add("key", "value");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Add(\"key\", \"value\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public void VoidMethodAbstract(object value)
        {
            var proxy = value.DuckCast<AbstractMethodRunner>();
            proxy.Add("key", "value");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Add(\"key\", \"value\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState VoidMethodConstraints(object value)
        {
            return _voidMethodIntegrationDelegate((PropertyPublicObject)value);
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Add(\"key\", \"value\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState VoidMethodIDuckTypeConstraints(object value)
        {
            return _voidMethodIDuckTypeIntegrationDelegate((PropertyPublicObject)value);
        }

        // Return Method

        [Benchmark]
        [BenchmarkCategory("proxy.Get(\"key\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public string ReturnMethodInterface(object value)
        {
            var proxy = value.DuckCast<IMethodRunner>();
            return proxy.Get("key");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Get(\"key\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public string ReturnMethodClass(object value)
        {
            var proxy = value.DuckCast<MethodRunnerClass>();
            return proxy.Get("key");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Get(\"key\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public string ReturnMethodAbstract(object value)
        {
            var proxy = value.DuckCast<AbstractMethodRunner>();
            return proxy.Get("key");
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Get(\"key\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState ReturnMethodConstraints(object value)
        {
            return _returnMethodIntegrationDelegate((PropertyPublicObject)value);
        }

        [Benchmark]
        [BenchmarkCategory("proxy.Get(\"key\")")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState ReturnMethodIDuckTypeConstraints(object value)
        {
            return _returnMethodIDuckTypeIntegrationDelegate((PropertyPublicObject)value);
        }

        // Get Properties

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public int GetPropertiesByInterfaces(object value)
        {
            var proxy = value.DuckCast<IMethodRunner>();
            var a = proxy.PublicGetValueType;
            var b = proxy.InternalGetValueType;
            var c = proxy.ProtectedGetValueType;
            var d = proxy.PrivateGetValueType;
            return a + b + c + d;
        }

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public int GetPropertiesByClasses(object value)
        {
            var proxy = value.DuckCast<MethodRunnerClass>();
            var a = proxy.PublicGetValueType;
            var b = proxy.InternalGetValueType;
            var c = proxy.ProtectedGetValueType;
            var d = proxy.PrivateGetValueType;
            return a + b + c + d;
        }

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public int GetPropertiesByAbstractClasses(object value)
        {
            var proxy = value.DuckCast<AbstractMethodRunner>();
            var a = proxy.PublicGetValueType;
            var b = proxy.InternalGetValueType;
            var c = proxy.ProtectedGetValueType;
            var d = proxy.PrivateGetValueType;
            return a + b + c + d;
        }

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public int GetPropertiesByDuckCopyStruct(object value)
        {
            var proxy = value.DuckCast<ValuesCopy>();
            var a = proxy.PublicGetValueType;
            var b = proxy.InternalGetValueType;
            var c = proxy.ProtectedGetValueType;
            var d = proxy.PrivateGetValueType;
            return a + b + c + d;
        }

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState GetPropertiesByConstraints(object value)
        {
            return _getPropertiesIntegrationDelegate((PropertyPublicObject)value);
        }

        [Benchmark]
        [BenchmarkCategory("GetProperties")]
        [ArgumentsSource(nameof(GetObjects))]
        public CallTargetState GetPropertiesByIDuckTypeConstraints(object value)
        {
            return _getPropertiesIDuckTypeIntegrationDelegate((PropertyPublicObject)value);
        }

        //

        public interface IMethodRunner
        {
            int PublicGetValueType { get; }

            int InternalGetValueType { get; }

            int ProtectedGetValueType { get; }

            int PrivateGetValueType { get; }

            void Add(string key, string value);

            string Get(string key);

            bool TryGetValue(string key, out string value);
        }

        public class MethodRunnerClass
        {
            public virtual int PublicGetValueType { get { return default; } }

            public virtual int InternalGetValueType { get { return default; } }

            public virtual int ProtectedGetValueType { get { return default; } }

            public virtual int PrivateGetValueType { get { return default; } }

            public virtual void Add(string key, string value) { }

            public virtual string Get(string key) => null;

            public virtual bool TryGetValue(string key, out string value)
            {
                value = null;
                return false;
            }
        }

        public abstract class AbstractMethodRunner
        {
            public abstract int PublicGetValueType { get; }

            public abstract int InternalGetValueType { get; }

            public abstract int ProtectedGetValueType { get; }

            public abstract int PrivateGetValueType { get; }

            public abstract void Add(string key, string value);

            public abstract string Get(string key);

            public abstract bool TryGetValue(string key, out string value);
        }

        [DuckCopy]
        public struct ValuesCopy
        {
            public int PublicGetValueType;
            public int InternalGetValueType;
            public int ProtectedGetValueType;
            public int PrivateGetValueType;
        }

        //

        public class VoidMethodIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner
            {
                instance.Add("key", "value");
                return CallTargetState.GetDefault();
            }
        }

        public class ReturnMethodIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner
            {
                _ = instance.Get("key");
                return CallTargetState.GetDefault();
            }
        }

        public class GetPropertiesIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner
            {
                var a = instance.PublicGetValueType;
                var b = instance.InternalGetValueType;
                var c = instance.ProtectedGetValueType;
                var d = instance.PrivateGetValueType;
                var z = a + b + c + d;
                return CallTargetState.GetDefault();
            }
        }

        //

        public class VoidMethodIDuckTypeIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner, IDuckType
            {
                if (instance.Instance is not null)
                {
                    instance.Add("key", "value");
                }

                return CallTargetState.GetDefault();
            }
        }

        public class ReturnMethodIDuckTypeIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner, IDuckType
            {
                if (instance.Instance is not null)
                {
                    _ = instance.Get("key");
                }

                return CallTargetState.GetDefault();
            }
        }

        public class GetPropertiesIDuckTypeIntegration
        {
            internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
                where TTarget : IMethodRunner, IDuckType
            {
                if (instance.Instance is not null)
                {
                    var a = instance.PublicGetValueType;
                    var b = instance.InternalGetValueType;
                    var c = instance.ProtectedGetValueType;
                    var d = instance.PrivateGetValueType;
                    var z = a + b + c + d;
                }

                return CallTargetState.GetDefault();
            }
        }
    }
}

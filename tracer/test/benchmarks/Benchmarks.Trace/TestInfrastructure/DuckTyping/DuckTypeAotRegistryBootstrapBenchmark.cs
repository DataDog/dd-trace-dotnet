// <copyright file="DuckTypeAotRegistryBootstrapBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Datadog.Trace.DuckTyping;

#pragma warning disable CS0618 // The benchmark intentionally mirrors generated registry calls to obsolete manual registration APIs.

namespace Benchmarks.Trace.DuckTyping
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class DuckTypeAotRegistryBootstrapBenchmark
    {
        private const int MarkerBitCount = 14;
        private static readonly MethodInfo CreateActivatorMethod = typeof(DuckTypeAotRegistryBootstrapBenchmark).GetMethod(nameof(CreateActivator), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo CreateValueReaderMethod = typeof(DuckTypeAotRegistryBootstrapBenchmark).GetMethod(nameof(CreateValueReader), BindingFlags.NonPublic | BindingFlags.Static)!;

        private BootstrapRegistration[] _registrations = null!;
        private string _datadogTraceAssemblyVersion = null!;
        private string _datadogTraceAssemblyMvid = null!;
        private string _registryAssemblyFullName = null!;
        private string _registryAssemblyMvid = null!;

        [Params(1, 16, 64, 256, 1024)]
        public int MappingCount { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            var traceAssembly = typeof(DuckType).Assembly;
            _datadogTraceAssemblyVersion = traceAssembly.GetName().Version?.ToString() ?? "0.0.0.0";
            _datadogTraceAssemblyMvid = traceAssembly.ManifestModule.ModuleVersionId.ToString("D");

            var registryAssembly = typeof(DuckTypeAotRegistryBootstrapBenchmark).Assembly;
            _registryAssemblyFullName = registryAssembly.FullName!;
            _registryAssemblyMvid = registryAssembly.ManifestModule.ModuleVersionId.ToString("D");

            _registrations = new BootstrapRegistration[MappingCount];
            for (var i = 0; i < _registrations.Length; i++)
            {
                var markerType = CreateMarkerType(i);
                _registrations[i] = new BootstrapRegistration(
                    typeof(IBootstrapProxy<>).MakeGenericType(markerType).TypeHandle,
                    typeof(BootstrapTarget<>).MakeGenericType(markerType).TypeHandle,
                    typeof(BootstrapGeneratedProxy<>).MakeGenericType(markerType).TypeHandle,
                    Activator.CreateInstance(typeof(BootstrapTarget<>).MakeGenericType(markerType), i)!,
                    (Func<object?, object?>)CreateActivatorMethod.MakeGenericMethod(markerType).Invoke(null, null)!,
                    (Func<object, int>)CreateValueReaderMethod.MakeGenericMethod(markerType).Invoke(null, null)!);
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            DuckType.ResetRuntimeModeForTests();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            DuckType.ResetRuntimeModeForTests();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            DuckType.ResetRuntimeModeForTests();
        }

        [BenchmarkCategory("Cold mapping startup")]
        [InvocationCount(1)]
        [Benchmark(Baseline = true)]
        public int DynamicFirstUse()
        {
            var sum = 0;
            foreach (var registration in _registrations)
            {
                var proxy = DuckType.Create(Type.GetTypeFromHandle(registration.ProxyDefinitionTypeHandle)!, registration.TargetInstance);
                sum += registration.ReadValue(proxy);
            }

            return sum;
        }

        [BenchmarkCategory("Cold mapping startup")]
        [InvocationCount(1)]
        [Benchmark]
        public int AotBootstrapAndFirstUse()
        {
            InitializeAotRegistry();

            var sum = 0;
            foreach (var registration in _registrations)
            {
                var proxy = DuckType.Create(Type.GetTypeFromHandle(registration.ProxyDefinitionTypeHandle)!, registration.TargetInstance);
                sum += registration.ReadValue(proxy);
            }

            return sum;
        }

        [BenchmarkCategory("AOT registry bootstrap only")]
        [InvocationCount(1)]
        [Benchmark]
        public int AotBootstrapOnly()
        {
            InitializeAotRegistry();
            return _registrations.Length;
        }

        private void InitializeAotRegistry()
        {
            DuckType.EnableAotMode();
            DuckType.ValidateAotRegistryContract(
                "1",
                _datadogTraceAssemblyVersion,
                _datadogTraceAssemblyMvid,
                _registryAssemblyFullName,
                _registryAssemblyMvid);

            foreach (var registration in _registrations)
            {
                DuckType.RegisterAotProxy(
                    Type.GetTypeFromHandle(registration.ProxyDefinitionTypeHandle)!,
                    Type.GetTypeFromHandle(registration.TargetTypeHandle)!,
                    Type.GetTypeFromHandle(registration.GeneratedProxyTypeHandle)!,
                    registration.CreateProxy);
            }
        }

        private static Type CreateMarkerType(int value)
        {
            var markerType = typeof(MarkerSeed);
            for (var bit = 0; bit < MarkerBitCount; bit++)
            {
                markerType = (value & (1 << bit)) == 0
                                 ? typeof(MarkerZero<>).MakeGenericType(markerType)
                                 : typeof(MarkerOne<>).MakeGenericType(markerType);
            }

            return markerType;
        }

        private static Func<object?, object?> CreateActivator<TMarker>()
        {
            return static instance => new BootstrapGeneratedProxy<TMarker>((BootstrapTarget<TMarker>)instance!);
        }

        private static Func<object, int> CreateValueReader<TMarker>()
        {
            return static proxy => ((IBootstrapProxy<TMarker>)proxy).Value;
        }

        private readonly struct BootstrapRegistration
        {
            public BootstrapRegistration(
                RuntimeTypeHandle proxyDefinitionTypeHandle,
                RuntimeTypeHandle targetTypeHandle,
                RuntimeTypeHandle generatedProxyTypeHandle,
                object targetInstance,
                Func<object?, object?> createProxy,
                Func<object, int> readValue)
            {
                ProxyDefinitionTypeHandle = proxyDefinitionTypeHandle;
                TargetTypeHandle = targetTypeHandle;
                GeneratedProxyTypeHandle = generatedProxyTypeHandle;
                TargetInstance = targetInstance;
                CreateProxy = createProxy;
                ReadValue = readValue;
            }

            public RuntimeTypeHandle ProxyDefinitionTypeHandle { get; }

            public RuntimeTypeHandle TargetTypeHandle { get; }

            public RuntimeTypeHandle GeneratedProxyTypeHandle { get; }

            public object TargetInstance { get; }

            public Func<object?, object?> CreateProxy { get; }

            public Func<object, int> ReadValue { get; }
        }

        public interface IBootstrapProxy<TMarker>
        {
            int Value { get; }
        }

        public sealed class BootstrapTarget<TMarker>
        {
            private readonly int _value;

            public BootstrapTarget(int value)
            {
                _value = value;
            }

            public int Value => _value;
        }

        public sealed class BootstrapGeneratedProxy<TMarker> : IBootstrapProxy<TMarker>
        {
            private readonly BootstrapTarget<TMarker> _target;

            public BootstrapGeneratedProxy(BootstrapTarget<TMarker> target)
            {
                _target = target;
            }

            public int Value => _target.Value;
        }

        private sealed class MarkerSeed
        {
        }

        private sealed class MarkerZero<TMarker>
        {
        }

        private sealed class MarkerOne<TMarker>
        {
        }
    }
}

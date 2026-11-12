// <copyright file="DuckTypeAotComparisonBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
    public class DuckTypeAotComparisonBenchmark
    {
        private const int CachedProxyReadOperations = 16;

        private BenchmarkDuckTarget _target = null!;
        private IBenchmarkDuckProxy _proxy = null!;

        [GlobalSetup(Target = nameof(DynamicDuckCastAndRead))]
        public void SetupDynamicDuckCastAndRead()
        {
            SetupDynamic();
        }

        [GlobalSetup(Target = nameof(DynamicNonGenericDuckCastAndRead))]
        public void SetupDynamicNonGenericDuckCastAndRead()
        {
            SetupDynamic();
        }

        [GlobalSetup(Target = nameof(AotDuckCastAndRead))]
        public void SetupAotDuckCastAndRead()
        {
            SetupAot();
        }

        [GlobalSetup(Target = nameof(AotNonGenericDuckCastAndRead))]
        public void SetupAotNonGenericDuckCastAndRead()
        {
            SetupAot();
        }

        [GlobalSetup(Target = nameof(DynamicProxyRead))]
        public void SetupDynamicProxyRead()
        {
            SetupDynamic();
            _proxy = _target.DuckCast<IBenchmarkDuckProxy>()!;
        }

        [GlobalSetup(Target = nameof(AotProxyRead))]
        public void SetupAotProxyRead()
        {
            SetupAot();
            _proxy = _target.DuckCast<IBenchmarkDuckProxy>()!;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            DuckType.ResetRuntimeModeForTests();
        }

        [BenchmarkCategory("DuckCast + property")]
        [Benchmark(Baseline = true)]
        public int DynamicDuckCastAndRead()
        {
            return _target.DuckCast<IBenchmarkDuckProxy>()!.Value;
        }

        [BenchmarkCategory("DuckCast + property")]
        [Benchmark]
        public int AotDuckCastAndRead()
        {
            return _target.DuckCast<IBenchmarkDuckProxy>()!.Value;
        }

        [BenchmarkCategory("DuckType.Create(Type) + property")]
        [Benchmark(Baseline = true)]
        public int DynamicNonGenericDuckCastAndRead()
        {
            return ((IBenchmarkDuckProxy)DuckType.Create(typeof(IBenchmarkDuckProxy), _target)).Value;
        }

        [BenchmarkCategory("DuckType.Create(Type) + property")]
        [Benchmark]
        public int AotNonGenericDuckCastAndRead()
        {
            return ((IBenchmarkDuckProxy)DuckType.Create(typeof(IBenchmarkDuckProxy), _target)).Value;
        }

        [BenchmarkCategory("Cached proxy property")]
        [Benchmark(Baseline = true, OperationsPerInvoke = CachedProxyReadOperations)]
        public int DynamicProxyRead()
        {
            var proxy = _proxy;
            var sum = 0;
            for (var i = 0; i < CachedProxyReadOperations; i++)
            {
                sum += proxy.Value;
            }

            return sum;
        }

        [BenchmarkCategory("Cached proxy property")]
        [Benchmark(OperationsPerInvoke = CachedProxyReadOperations)]
        public int AotProxyRead()
        {
            var proxy = _proxy;
            var sum = 0;
            for (var i = 0; i < CachedProxyReadOperations; i++)
            {
                sum += proxy.Value;
            }

            return sum;
        }

        private void SetupDynamic()
        {
            DuckType.ResetRuntimeModeForTests();
            _target = new BenchmarkDuckTarget(42);
            _ = _target.DuckCast<IBenchmarkDuckProxy>()!.Value;
        }

        private void SetupAot()
        {
            DuckType.ResetRuntimeModeForTests();
            DuckType.EnableAotMode();
            var traceAssembly = typeof(DuckType).Assembly;
            var registryAssembly = typeof(DuckTypeAotComparisonBenchmark).Assembly;
            DuckType.ValidateAotRegistryContract(
                "1",
                traceAssembly.GetName().Version?.ToString() ?? "0.0.0.0",
                traceAssembly.ManifestModule.ModuleVersionId.ToString("D"),
                registryAssembly.FullName!,
                registryAssembly.ManifestModule.ModuleVersionId.ToString("D"));
            DuckType.RegisterAotProxy(
                typeof(IBenchmarkDuckProxy),
                typeof(BenchmarkDuckTarget),
                typeof(BenchmarkDuckAotProxy),
                new Func<object?, object?>(CreateBenchmarkDuckAotProxy));

            _target = new BenchmarkDuckTarget(42);
            _ = _target.DuckCast<IBenchmarkDuckProxy>()!.Value;
        }

        private static object? CreateBenchmarkDuckAotProxy(object? instance)
        {
            return new BenchmarkDuckAotProxy((BenchmarkDuckTarget)instance!);
        }

        public interface IBenchmarkDuckProxy
        {
            int Value { get; }
        }

        public sealed class BenchmarkDuckTarget
        {
            private int _value;

            public BenchmarkDuckTarget(int value)
            {
                _value = value;
            }

            public int Value => _value;
        }

        public readonly struct BenchmarkDuckAotProxy : IBenchmarkDuckProxy
        {
            private readonly BenchmarkDuckTarget _target;

            public BenchmarkDuckAotProxy(BenchmarkDuckTarget target)
            {
                _target = target;
            }

            public int Value => _target.Value;
        }
    }
}

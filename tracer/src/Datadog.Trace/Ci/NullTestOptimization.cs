// <copyright file="NullTestOptimization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal class NullTestOptimization : ITestOptimization
{
    public IDatadogLogger Log => DatadogSerilogLogger.NullLogger;

    public bool Enabled => false;

    public bool IsRunning => false;

    // TODO: shouldn't ever call this
    public TestOptimizationSettings Settings => null!;

    // TODO: shouldn't ever call this
    public ITestOptimizationClient Client => null!;

    // TODO: shouldn't ever call this
    public ITestOptimizationHostInfo HostInfo => null!;

    public ITestOptimizationTracerManagement? TracerManagement => null;

    public ITestOptimizationKnownTestsFeature? KnownTestsFeature => null;

    public ITestOptimizationEarlyFlakeDetectionFeature? EarlyFlakeDetectionFeature => null;

    public ITestOptimizationSkippableFeature? SkippableFeature => null;

    public ITestOptimizationImpactedTestsDetectionFeature? ImpactedTestsDetectionFeature => null;

    public ITestOptimizationFlakyRetryFeature? FlakyRetryFeature => null;

    public ITestOptimizationDynamicInstrumentationFeature? DynamicInstrumentationFeature => null;

    public ITestOptimizationTestManagementFeature? TestManagementFeature => null;

    public CIEnvironmentValues CIValues => null!;

    public void InitializeFromRunner(TestOptimizationSettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled, bool? useLockedTracerManager = null)
    {
    }

    public void InitializeFromManualInstrumentation()
    {
    }

    public void Flush()
    {
    }

    public Task FlushAsync() => Task.CompletedTask;

    public void Close()
    {
    }

    public void Reset()
    {
    }
}

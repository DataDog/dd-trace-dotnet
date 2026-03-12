// <copyright file="ITestOptimization.cs" company="Datadog">
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

internal interface ITestOptimization
{
    IDatadogLogger Log { get; }

    bool Enabled { get; }

    bool IsRunning { get; }

    string RunId { get; }

    TestOptimizationSettings Settings { get; }

    ITestOptimizationClient Client { get; }

    ITestOptimizationHostInfo HostInfo { get; }

    ITestOptimizationTracerManagement? TracerManagement { get; }

    ITestOptimizationKnownTestsFeature? KnownTestsFeature { get; }

    ITestOptimizationEarlyFlakeDetectionFeature? EarlyFlakeDetectionFeature { get; }

    ITestOptimizationSkippableFeature? SkippableFeature { get; }

    ITestOptimizationImpactedTestsDetectionFeature? ImpactedTestsDetectionFeature { get; }

    ITestOptimizationFlakyRetryFeature? FlakyRetryFeature { get; }

    ITestOptimizationDynamicInstrumentationFeature? DynamicInstrumentationFeature { get; }

    ITestOptimizationTestManagementFeature? TestManagementFeature { get; }

    CIEnvironmentValues CIValues { get; }

    void Initialize();

    void InitializeFromRunner(TestOptimizationSettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled, bool? useLockedTracerManager = null);

    void InitializeFromManualInstrumentation();

    void Flush();

    Task FlushAsync();

    void Close();

    void Reset();
}

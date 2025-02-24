// <copyright file="ICiVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal interface ICiVisibility
{
    IDatadogLogger Log { get; }

    bool Enabled { get; }

    bool IsRunning { get; }

    CIVisibilitySettings Settings { get; }

    ITestOptimizationClient Client { get; }

    ICiVisibilityTracerManagement TracerManagement { get; }

    ICiVisibilityHostInfo HostInfo { get; }

    ICiVisibilityEarlyFlakeDetectionFeature EarlyFlakeDetectionFeature { get; }

    ICiVisibilitySkippableFeature SkippableFeature { get; }

    ICiVisibilityImpactedTestsDetectionFeature ImpactedTestsDetectionFeature { get; }

    void Initialize();

    void InitializeFromRunner(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool eventPlatformProxyEnabled);

    void InitializeFromManualInstrumentation();

    void Flush();

    Task FlushAsync();

    void Close();

    void Reset();
}

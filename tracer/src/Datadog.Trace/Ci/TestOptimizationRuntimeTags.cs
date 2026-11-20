// <copyright file="TestOptimizationRuntimeTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.Ci;

/// <summary>
/// OS and runtime tags reported to Test Optimization: the detected values with the DD_TEST_OPTIMIZATION_RUNTIME_TAGS overrides applied.
/// </summary>
internal sealed class TestOptimizationRuntimeTags
{
    private TestOptimizationRuntimeTags(ITestOptimization testOptimization)
    {
        var framework = FrameworkDescription.Instance;
        var overrides = testOptimization.Settings.RuntimeTags;
        OSPlatform = Get(overrides, CommonTags.OSPlatform, framework.OSPlatform);
        OSVersion = Get(overrides, CommonTags.OSVersion, testOptimization.HostInfo.GetOperatingSystemVersion());
        OSArchitecture = Get(overrides, CommonTags.OSArchitecture, framework.OSArchitecture);
        RuntimeName = Get(overrides, CommonTags.RuntimeName, framework.Name);
        RuntimeVersion = Get(overrides, CommonTags.RuntimeVersion, framework.ProductVersion);
        RuntimeArchitecture = Get(overrides, CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
    }

    public string OSPlatform { get; }

    public string OSVersion { get; }

    public string OSArchitecture { get; }

    public string RuntimeName { get; }

    public string RuntimeVersion { get; }

    public string RuntimeArchitecture { get; }

    public static TestOptimizationRuntimeTags Create(ITestOptimization testOptimization) => new(testOptimization);

    private static string Get(IReadOnlyDictionary<string, string> overrides, string tag, string detected)
        => overrides.TryGetValue(tag, out var value) ? value : detected;
}

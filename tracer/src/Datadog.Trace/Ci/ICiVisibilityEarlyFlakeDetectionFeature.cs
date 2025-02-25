// <copyright file="ICiVisibilityEarlyFlakeDetectionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.Net;

namespace Datadog.Trace.Ci;

internal interface ICiVisibilityEarlyFlakeDetectionFeature : ICiVisibilityFeature
{
    TestOptimizationClient.EarlyFlakeDetectionSettingsResponse EarlyFlakeDetectionSettings { get; }

    TestOptimizationClient.EarlyFlakeDetectionResponse? EarlyFlakeDetectionResponse { get; }

    bool IsAnEarlyFlakeDetectionTest(string moduleName, string testSuite, string testName);
}

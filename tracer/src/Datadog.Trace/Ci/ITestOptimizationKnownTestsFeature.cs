// <copyright file="ITestOptimizationKnownTestsFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.Net;

namespace Datadog.Trace.Ci;

internal interface ITestOptimizationKnownTestsFeature : ITestOptimizationFeature
{
    TestOptimizationClient.KnownTestsResponse? KnownTests { get; }

    bool IsAKnownTest(string moduleName, string testSuite, string testName);
}

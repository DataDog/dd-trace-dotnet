// <copyright file="BacklogTagCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class BacklogTagCacheTests : TagCacheTestsBase
{
    private protected override object GetOverflowTestTags<TScenario>(DataStreamsManager manager, int value)
        => manager.GetOrCreateBacklogTags(new OverflowTestKey<TScenario>(value), static k => $"tag:{k.Value}");
}

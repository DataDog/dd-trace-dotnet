// <copyright file="FlakyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Xunit;

/// <summary>
/// Marks a test as flaky, so that it is automatically retried by <see cref="ProfilerTestCase"/>.
/// </summary>
/// <param name="reason">The reason that this test was marked flaky.</param>
/// <param name="maxRetries">The maximum number of times a test should be retried (default 10).</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class FlakyAttribute(string reason, byte maxRetries = 5) : Attribute
{
    public string Reason { get; } = reason;

    public byte MaxRetries { get; } = maxRetries;
}

// <copyright file="FlakyAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Marks a test as flaky, so that it is automatically retried by <see cref="CustomTestFramework"/>
/// </summary>
/// <param name="reason">The reason that this test was marked flaky e.g. it relies on flaky infrastructure, hits a known runtime bug etc</param>
/// <param name="maxRetries">The maximum number of times a test should be retried</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class FlakyAttribute(string reason, int maxRetries = 1) : Attribute
{
    public string Reason { get; } = reason;

    public int MaxRetries { get; } = maxRetries;
}

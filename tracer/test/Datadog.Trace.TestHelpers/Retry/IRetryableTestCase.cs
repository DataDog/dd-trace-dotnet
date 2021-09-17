// <copyright file="IRetryableTestCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.dev/JoshKeegan/xRetry

using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers.Retry
{
    public interface IRetryableTestCase : IXunitTestCase
    {
        int MaxRetries { get; }

        int DelayBetweenRetriesMs { get; }
    }
}

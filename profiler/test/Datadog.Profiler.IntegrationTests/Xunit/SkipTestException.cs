// <copyright file="SkipTestException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests.Xunit;

/// <summary>
/// Thrown by a test that cannot run on the current host.
/// <see cref="SkippableMessageBus"/> turns the resulting failure into a skipped test.
/// </summary>
internal class SkipTestException : Exception
{
    public SkipTestException(string reason)
        : base(reason)
    {
    }
}

// <copyright file="SkippableMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit;

/// <summary>
/// Rewrites the failure of a test that threw <see cref="SkipTestException"/> into a skipped test.
/// xUnit v2 has no way to skip a test once it has started, so the test throws and the decision
/// is translated here, on the way to the inner bus.
/// </summary>
internal class SkippableMessageBus : IMessageBus
{
    private readonly IMessageBus _innerBus;
    private int _skippedCount;

    public SkippableMessageBus(IMessageBus innerBus)
    {
        _innerBus = innerBus;
    }

    /// <summary>
    /// Gets the number of failures that were turned into skips.
    /// </summary>
    public int SkippedCount => _skippedCount;

    public bool QueueMessage(IMessageSinkMessage message)
    {
        if (message is ITestFailed failed && TryGetSkipReason(failed, out var reason))
        {
            Interlocked.Increment(ref _skippedCount);
            return _innerBus.QueueMessage(new TestSkipped(failed.Test, reason));
        }

        return _innerBus.QueueMessage(message);
    }

    public void Dispose()
    {
        // the inner bus is owned by the caller
    }

    private static bool TryGetSkipReason(ITestFailed failed, out string reason)
    {
        var skipExceptionName = typeof(SkipTestException).FullName;

        for (var i = 0; i < failed.ExceptionTypes.Length; i++)
        {
            if (failed.ExceptionTypes[i] == skipExceptionName)
            {
                reason = i < failed.Messages.Length ? failed.Messages[i] : "Test skipped";
                return true;
            }
        }

        reason = null;
        return false;
    }
}

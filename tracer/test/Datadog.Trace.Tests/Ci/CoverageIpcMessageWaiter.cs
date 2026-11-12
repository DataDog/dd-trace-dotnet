// <copyright file="CoverageIpcMessageWaiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using FluentAssertions;

namespace Datadog.Trace.Tests.Ci;

internal sealed class CoverageIpcMessageWaiter
{
    private static readonly FieldInfo CoverageMessageCountField = GetRequiredField("_coverageIpcMessageCount");
    private static readonly FieldInfo ActiveCallbacksField = GetRequiredField("_activeIpcCallbacks");
    private readonly TestSession _session;
    private readonly int _initialMessageCount;

    // Capture before sending: the IPC callback may finish before the test starts waiting.
    public CoverageIpcMessageWaiter(TestSession session)
    {
        _session = session;
        _initialMessageCount = GetMessageCount();
    }

    public void WaitForMessages(TimeSpan timeout, int messageCount = 1)
    {
        var expectedMessageCount = _initialMessageCount + messageCount;
        // The coverage counter advances before the callback records its result.
        var completed = SpinWait.SpinUntil(
            () => GetMessageCount() >= expectedMessageCount && GetActiveCallbacks() == 0,
            timeout);

        completed.Should().BeTrue(
            "{0} coverage IPC message(s) should finish processing within {1} (initial count: {2}, current count: {3}, active callbacks: {4})",
            messageCount,
            timeout,
            _initialMessageCount,
            GetMessageCount(),
            GetActiveCallbacks());
    }

    private static FieldInfo GetRequiredField(string name)
        => typeof(TestSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(TestSession), name);

    private int GetMessageCount() => ReadCounter(CoverageMessageCountField);

    private int GetActiveCallbacks() => ReadCounter(ActiveCallbacksField);

    private int ReadCounter(FieldInfo field)
    {
        if (field.GetValue(_session) is not int value)
        {
            throw new InvalidOperationException($"Expected {nameof(TestSession)}.{field.Name} to be an integer counter.");
        }

        // Reflection supplies no acquire semantics. Order each load against subsequent reads
        // so the callback's Interlocked updates and completed work are observed in order.
        Thread.MemoryBarrier();
        return value;
    }
}

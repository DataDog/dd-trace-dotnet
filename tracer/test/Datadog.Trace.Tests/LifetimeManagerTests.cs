// <copyright file="LifetimeManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class LifetimeManagerTests
{
    public enum HookFailure
    {
        SyncHookThrows,
        AsyncHookFaults,
        AsyncHookTimesOut,
    }

    [Theory]
    [CombinatorialData]
    public void RunShutdownHooks_RunsRemainingHooksAfterAFailingHook(HookFailure failure)
    {
        var shutdownException = new InvalidOperationException("shutdown");
        var ran = new List<string>();
        var hooks = new ConcurrentQueue<object>();

        hooks.Enqueue(SyncHook("before"));
        hooks.Enqueue(FailingHook(failure));
        hooks.Enqueue(SyncHook("sync after"));
        hooks.Enqueue(AsyncHook("async after"));

        // Only the hook that never completes needs a short timeout. The other cases use a long one, so
        // a timer never decides their outcome. The healthy async hook completes synchronously, so no
        // timeout applies to it.
        var timeout = failure == HookFailure.AsyncHookTimesOut ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(30);
        LifetimeManager.RunShutdownHooks(hooks, shutdownException, timeout);

        ran.Should().Equal("before", "sync after", "async after");
        hooks.Should().BeEmpty();

        Action<Exception?> SyncHook(string name) => ex =>
        {
            ex.Should().BeSameAs(shutdownException);
            ran.Add(name);
        };

        Func<Exception?, Task> AsyncHook(string name) => ex =>
        {
            ex.Should().BeSameAs(shutdownException);
            ran.Add(name);
            return Task.CompletedTask;
        };
    }

    private static object FailingHook(HookFailure failure) => failure switch
    {
        HookFailure.SyncHookThrows => (Action<Exception?>)(_ => throw new Exception("sync hook failed")),
        HookFailure.AsyncHookFaults => (Func<Exception?, Task>)(async _ =>
        {
            await Task.Yield();
            throw new Exception("async hook failed");
        }),
        HookFailure.AsyncHookTimesOut => (Func<Exception?, Task>)(_ => new TaskCompletionSource<bool>().Task),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };
}

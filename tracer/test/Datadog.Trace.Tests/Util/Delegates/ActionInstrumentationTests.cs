// <copyright file="ActionInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

public class ActionInstrumentationTests
{
    [Fact]
    public void Action0Test()
    {
        var value = 0;
        Action action = () => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action0Callbacks
            {
                OnDelegateBegin = target =>
                {
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action();
        value.Should().Be(3);
    }

    [Fact]
    public void Action1Test()
    {
        var value = 0;
        Action<string> action = (arg1) => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action1Callbacks
            {
                OnDelegateBegin = (target, arg1) =>
                {
                    arg1.Should().Be("Arg01");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action("Arg01");
        value.Should().Be(3);
    }

    [Fact]
    public void Action2Test()
    {
        var value = 0;
        Action<string, string> action = (arg1, arg2) => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action2Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action("Arg01", "Arg02");
        value.Should().Be(3);
    }

    [Fact]
    public void Action3Test()
    {
        var value = 0;
        Action<string, string, string> action = (arg1, arg2, arg3) => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action3Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2, arg3) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    arg3.Should().Be("Arg03");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action("Arg01", "Arg02", "Arg03");
        value.Should().Be(3);
    }

    [Fact]
    public void Action4Test()
    {
        var value = 0;
        Action<string, string, string, string> action = (arg1, arg2, arg3, arg4) => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action4Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2, arg3, arg4) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    arg3.Should().Be("Arg03");
                    arg4.Should().Be("Arg04");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action("Arg01", "Arg02", "Arg03", "Arg04");
        value.Should().Be(3);
    }

    [Fact]
    public void Action5Test()
    {
        var value = 0;
        Action<string, string, string, string, string> action = (arg1, arg2, arg3, arg4, arg5) => { Interlocked.Increment(ref value); };
        action = ActionInstrumentation.Wrap(
            action,
            new Action5Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2, arg3, arg4, arg5) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    arg3.Should().Be("Arg03");
                    arg4.Should().Be("Arg04");
                    arg5.Should().Be("Arg05");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                },
            });

        action("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        value.Should().Be(3);
    }
}

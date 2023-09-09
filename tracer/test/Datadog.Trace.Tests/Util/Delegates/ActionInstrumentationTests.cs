// <copyright file="ActionInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

#pragma warning disable SA1201

public class ActionInstrumentationTests
{
    [Fact]
    public void Action0Test()
    {
        var callbacks = new Action0Callbacks();
        Action action = () => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action();
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        Action action2 = () => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = ActionInstrumentation.Wrap(action2, new DefaultAction0Callbacks(
                                                 target =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2();
        value.Should().Be(3);
    }

    public readonly struct Action0Callbacks : IAction0Callbacks
    {
        public Action0Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin(object sender)
        {
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Action1Test()
    {
        var callbacks = new Action1Callbacks();
        Action<string> action = (arg1) => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action("Arg01");
        callbacks.Count.Value.Should().Be(3);
    }

    public readonly struct Action1Callbacks : IAction1Callbacks
    {
        public Action1Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1>(object sender, ref TArg1 arg1)
        {
            arg1.Should().Be("Arg01");
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Action2Test()
    {
        var callbacks = new Action2Callbacks();
        Action<string, string> action = (arg1, arg2) => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action("Arg01", "Arg02");
        callbacks.Count.Value.Should().Be(3);
    }

    public readonly struct Action2Callbacks : IAction2Callbacks
    {
        public Action2Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1, TArg2>(object sender, ref TArg1 arg1, ref TArg2 arg2)
        {
            arg1.Should().Be("Arg01");
            arg2.Should().Be("Arg02");
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Action3Test()
    {
        var callbacks = new Action3Callbacks();
        Action<string, string, string> action = (arg1, arg2, arg3) => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action("Arg01", "Arg02", "Arg03");
        callbacks.Count.Value.Should().Be(3);
    }

    public readonly struct Action3Callbacks : IAction3Callbacks
    {
        public Action3Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1, TArg2, TArg3>(object sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3)
        {
            arg1.Should().Be("Arg01");
            arg2.Should().Be("Arg02");
            arg3.Should().Be("Arg03");
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Action4Test()
    {
        var callbacks = new Action4Callbacks();
        Action<string, string, string, string> action = (arg1, arg2, arg3, arg4) => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04");
        callbacks.Count.Value.Should().Be(3);
    }

    public readonly struct Action4Callbacks : IAction4Callbacks
    {
        public Action4Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4)
        {
            arg1.Should().Be("Arg01");
            arg2.Should().Be("Arg02");
            arg3.Should().Be("Arg03");
            arg4.Should().Be("Arg04");
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Action5Test()
    {
        var callbacks = new Action5Callbacks();
        Action<string, string, string, string, string> action = (arg1, arg2, arg3, arg4, arg5) => { callbacks.Count.Value++; };
        action = ActionInstrumentation.Wrap(action, callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        callbacks.Count.Value.Should().Be(3);
    }

    public readonly struct Action5Callbacks : IAction5Callbacks
    {
        public Action5Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5)
        {
            arg1.Should().Be("Arg01");
            arg2.Should().Be("Arg02");
            arg3.Should().Be("Arg03");
            arg4.Should().Be("Arg04");
            arg5.Should().Be("Arg05");
            Count.Value++;
            return null;
        }

        public void OnDelegateEnd(object sender, Exception exception, object state)
        {
            Count.Value++;
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }
}

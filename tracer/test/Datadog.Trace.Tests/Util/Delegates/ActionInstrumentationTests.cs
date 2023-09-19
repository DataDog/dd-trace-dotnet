// <copyright file="ActionInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

#pragma warning disable SA1201

public class ActionInstrumentationTests
{
    private delegate void CustomAction();

    private delegate void CustomAction<T>(T arg1);

    private delegate void CustomAction<T, T2>(T arg1, T2 arg2);

    private delegate void CustomAction<T, T2, T3>(T arg1, T2 arg2, T3 arg3);

    private delegate void CustomAction<T, T2, T3, T4>(T arg1, T2 arg2, T3 arg3, T4 arg4);

    private delegate void CustomAction<T, T2, T3, T4, T5>(T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    [Fact]
    public void Action0Test()
    {
        var callbacks = new Action0Callbacks();
        Action action = () => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action();
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction action2 = () => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction0Callbacks(
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

    public readonly struct Action0Callbacks : IBegin0Callbacks, IVoidReturnCallback
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
    public void NullAction0Test()
    {
        var callbacks = new Action0Callbacks();
        Action action = null;
        action = (Action)DelegateInstrumentation.Wrap(null, typeof(Action), callbacks);
        action();
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action), callbacks).DynamicInvoke();
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Action1Test()
    {
        var callbacks = new Action1Callbacks();
        Action<string> action = (arg1) => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action("Arg01");
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction<string> action2 = (arg1) => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2("Arg01");
        value.Should().Be(3);
    }

    public readonly struct Action1Callbacks : IBegin1Callbacks, IVoidReturnCallback
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
    public void NullAction1Test()
    {
        var callbacks = new Action1Callbacks();
        Action<string> action = null;
        action = (Action<string>)DelegateInstrumentation.Wrap(null, typeof(Action<string>), callbacks);
        action("Arg01");
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action<string>), callbacks).DynamicInvoke("Arg01");
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Action2Test()
    {
        var callbacks = new Action2Callbacks();
        Action<string, string> action = (arg1, arg2) => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action("Arg01", "Arg02");
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction<string, string> action2 = (arg1, arg2) => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction2Callbacks(
                                                 (target, arg1, arg2) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2("Arg01", "Arg02");
        value.Should().Be(3);
    }

    public readonly struct Action2Callbacks : IBegin2Callbacks, IVoidReturnCallback
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
    public void NullAction2Test()
    {
        var callbacks = new Action2Callbacks();
        Action<string, string> action = null;
        action = (Action<string, string>)DelegateInstrumentation.Wrap(null, typeof(Action<string, string>), callbacks);
        action("Arg01", "Arg02");
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action<string, string>), callbacks).DynamicInvoke("Arg01", "Arg02");
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Action3Test()
    {
        var callbacks = new Action3Callbacks();
        Action<string, string, string> action = (arg1, arg2, arg3) => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action("Arg01", "Arg02", "Arg03");
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction<string, string, string> action2 = (arg1, arg2, arg3) => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction3Callbacks(
                                                 (target, arg1, arg2, arg3) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     arg3.Should().Be("Arg03");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2("Arg01", "Arg02", "Arg03");
        value.Should().Be(3);
    }

    public readonly struct Action3Callbacks : IBegin3Callbacks, IVoidReturnCallback
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
    public void NullAction3Test()
    {
        var callbacks = new Action3Callbacks();
        Action<string, string, string> action = null;
        action = (Action<string, string, string>)DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string>), callbacks);
        action("Arg01", "Arg02", "Arg03");
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string>), callbacks).DynamicInvoke("Arg01", "Arg02", "Arg03");
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Action4Test()
    {
        var callbacks = new Action4Callbacks();
        Action<string, string, string, string> action = (arg1, arg2, arg3, arg4) => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04");
        callbacks.Count.Value.Should().Be(3);

        callbacks.Count.Value = 0;
        var delAction = (Delegate)new Action<string, string, string, string>((arg1, arg2, arg3, arg4) => { callbacks.Count.Value++; });
        delAction = delAction.Instrument(callbacks);
        delAction.DynamicInvoke("Arg01", "Arg02", "Arg03", "Arg04");
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction<string, string, string, string> action2 = (arg1, arg2, arg3, arg4) => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction4Callbacks(
                                                 (target, arg1, arg2, arg3, arg4) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     arg3.Should().Be("Arg03");
                                                     arg4.Should().Be("Arg04");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2("Arg01", "Arg02", "Arg03", "Arg04");
        value.Should().Be(3);
    }

    public readonly struct Action4Callbacks : IBegin4Callbacks, IVoidReturnCallback
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
    public void NullAction4Test()
    {
        var callbacks = new Action4Callbacks();
        Action<string, string, string, string> action = null;
        action = (Action<string, string, string, string>)DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string, string>), callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04");
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string, string>), callbacks).DynamicInvoke("Arg01", "Arg02", "Arg03", "Arg04");
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Action5Test()
    {
        var callbacks = new Action5Callbacks();
        Action<string, string, string, string, string> action = (arg1, arg2, arg3, arg4, arg5) => { callbacks.Count.Value++; };
        action = action.Instrument(callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomAction<string, string, string, string, string> action2 = (arg1, arg2, arg3, arg4, arg5) => { Interlocked.Increment(ref value).Should().Be(2); };
        action2 = DelegateInstrumentation.Wrap(action2, new DelegateAction5Callbacks(
                                                 (target, arg1, arg2, arg3, arg4, arg5) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     arg3.Should().Be("Arg03");
                                                     arg4.Should().Be("Arg04");
                                                     arg5.Should().Be("Arg05");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                 }));
        action2("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        value.Should().Be(3);
    }

    public readonly struct Action5Callbacks : IBegin5Callbacks, IVoidReturnCallback
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

    [Fact]
    public void NullAction5Test()
    {
        var callbacks = new Action5Callbacks();
        Action<string, string, string, string, string> action = null;
        action = (Action<string, string, string, string, string>)DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string, string, string>), callbacks);
        action("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Action<string, string, string, string, string>), callbacks).DynamicInvoke("Arg01", "Arg02", "Arg03", "Arg04", "Arg05");
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void FromDucktypeValueWithTypeTest()
    {
        // With an actual delegate in the original type
        var originalType = new OriginalType();
        var proxyType = originalType.DuckCast<IProxyType>();
        var callbacks = new Action1Callbacks();
        originalType.MyDelegate = (arg1) => { callbacks.Count.Value++; };
        originalType.MyDelegate.Should().NotBeNull();
        proxyType.MyDelegate = proxyType.MyDelegate.Instrument(callbacks);

        originalType.MyDelegate("Arg01");
        callbacks.Count.Value.Should().Be(3);

        // With Null value in the original type
        originalType = new OriginalType();
        originalType.MyDelegate.Should().BeNull();
        proxyType = originalType.DuckCast<IProxyType>();
        callbacks = new Action1Callbacks();
        proxyType.MyDelegate = proxyType.MyDelegate.Instrument(callbacks);

        originalType.MyDelegate("Arg01");
        callbacks.Count.Value.Should().Be(2);
    }

    private class OriginalType
    {
        public CustomAction<string> MyDelegate { get; set; }
    }

    internal interface IProxyType
    {
        public ValueWithType<Delegate> MyDelegate { get; set; }
    }
}

// <copyright file="FuncInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

#pragma warning disable SA1201

public class FuncInstrumentationTests
{
    private delegate TReturn CustomFunc<TReturn>();

    private delegate TReturn CustomFunc<T, TReturn>(T arg1);

    private delegate TReturn CustomFunc<T, T2, TReturn>(T arg1, T2 arg2);

    private delegate TReturn CustomFunc<T, T2, T3, TReturn>(T arg1, T2 arg2, T3 arg3);

    private delegate TReturn CustomFunc<T, T2, T3, T4, TReturn>(T arg1, T2 arg2, T3 arg3, T4 arg4);

    private delegate TReturn CustomFunc<T, T2, T3, T4, T5, TReturn>(T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    [Fact]
    public void Func0Test()
    {
        var callbacks = new Func0Callbacks();
        Func<int> func = () =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func().Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<int> func2 = () =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc0Callbacks(
                                                 target =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2().Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func0CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<int> func = () =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc0Callbacks(
                                                 target =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func().Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func0Callbacks : IBegin0Callbacks, IReturnCallback
    {
        public Func0Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin(object sender)
        {
            Count.Value++;
            return null;
        }

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void NullFunc0Test()
    {
        var callbacks = new Func0Callbacks();
        Func<int> func = null;
        func = (Func<int>)DelegateInstrumentation.Wrap(null, typeof(Func<int>), callbacks);
        func();
        callbacks.Count.Value.Should().Be(2);

        // with no casting
        callbacks.Count.Value = 0;
        DelegateInstrumentation.Wrap(null, typeof(Func<int>), callbacks).DynamicInvoke();
        callbacks.Count.Value.Should().Be(2);
    }

    [Fact]
    public void Func1Test()
    {
        var callbacks = new Func1Callbacks();
        Func<string, int> func = (arg1) =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func("Arg01").Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<string, int> func2 = (arg1) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2("Arg01").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func1CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<string, int> func = _ =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc1Callbacks(
                                                 (target, _) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func(default).Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func1Callbacks : IBegin1Callbacks, IReturnCallback
    {
        public Func1Callbacks()
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

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Func2Test()
    {
        var callbacks = new Func2Callbacks();
        Func<string, string, int> func = (arg1, arg2) =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func("Arg01", "Arg02").Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<string, string, int> func2 = (arg1, arg2) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc2Callbacks(
                                                 (target, arg1, arg2) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2("Arg01", "Arg02").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func2CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<string, string, int> func = (_, _) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc2Callbacks(
                                                 (target, _, _) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func(default, default).Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func2Callbacks : IBegin2Callbacks, IReturnCallback
    {
        public Func2Callbacks()
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

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Func3Test()
    {
        var callbacks = new Func3Callbacks();
        Func<string, string, string, int> func = (arg1, arg2, arg3) =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func("Arg01", "Arg02", "Arg03").Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<string, string, string, int> func2 = (arg1, arg2, arg3) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc3Callbacks(
                                                 (target, arg1, arg2, arg3) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     arg3.Should().Be("Arg03");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2("Arg01", "Arg02", "Arg03").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func3CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<string, string, string, int> func = (_, _, _) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc3Callbacks(
                                                 (target, _, _, _) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func(default, default, default).Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func3Callbacks : IBegin3Callbacks, IReturnCallback
    {
        public Func3Callbacks()
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

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Func4Test()
    {
        var callbacks = new Func4Callbacks();
        Func<string, string, string, string, int> func = (arg1, arg2, arg3, arg4) =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func("Arg01", "Arg02", "Arg03", "Arg04").Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<string, string, string, string, int> func2 = (arg1, arg2, arg3, arg4) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc4Callbacks(
                                                 (target, arg1, arg2, arg3, arg4) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     arg2.Should().Be("Arg02");
                                                     arg3.Should().Be("Arg03");
                                                     arg4.Should().Be("Arg04");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2("Arg01", "Arg02", "Arg03", "Arg04").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func4CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<string, string, string, string, int> func = (_, _, _, _) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc4Callbacks(
                                                 (target, _, _, _, _) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func(default, default, default, default).Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func4Callbacks : IBegin4Callbacks, IReturnCallback
    {
        public Func4Callbacks()
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

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public void Func5Test()
    {
        var callbacks = new Func5Callbacks();
        Func<string, string, string, string, string, int> func = (arg1, arg2, arg3, arg4, arg5) =>
        {
            callbacks.Count.Value++;
            return 42;
        };
        func = func.Instrument(callbacks);
        func("Arg01", "Arg02", "Arg03", "Arg04", "Arg05").Should().Be(43);
        callbacks.Count.Value.Should().Be(3);

        var value = 0;
        CustomFunc<string, string, string, string, string, int> func2 = (arg1, arg2, arg3, arg4, arg5) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc5Callbacks(
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
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        func2("Arg01", "Arg02", "Arg03", "Arg04", "Arg05").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func5CallbackFailureTest()
    {
        var value = 0;
        CustomFunc<string, string, string, string, string, int> func = (_, _, _, _, _) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = DelegateInstrumentation.Wrap(func, new DelegateFunc5Callbacks(
                                                 (target, _, _, _, _, _) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        using var scope = new AssertionScope();
        func(default, default, default, default, default).Should().Be(42);
        value.Should().Be(3);
    }

    public readonly struct Func5Callbacks : IBegin5Callbacks, IReturnCallback
    {
        public Func5Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public object OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5)
        {
            using var scope = new AssertionScope();
            arg1.Should().Be("Arg01");
            arg2.Should().Be("Arg02");
            arg3.Should().Be("Arg03");
            arg4.Should().Be("Arg04");
            arg5.Should().Be("Arg05");
            Count.Value++;
            return null;
        }

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (TReturn)(object)((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }

    [Fact]
    public async Task Async1Test()
    {
        var callbacks = new Async1Callbacks();
        Func<string, Task<int>> func = async (arg1) =>
        {
            callbacks.Count.Value++;
            await Task.Delay(100).ConfigureAwait(false);
            return 42;
        };
        func = func.Instrument(callbacks);
        var result = await func("Arg01").ConfigureAwait(false);
        result.Should().Be(43);
        callbacks.Count.Value.Should().Be(4);

        var value = 0;
        CustomFunc<string, Task<int>> func2 = async (arg1) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            await Task.Delay(100).ConfigureAwait(false);
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     // 3 or 4 because we can't predict if onDelegateEnd or onDelegateAsyncEnd will be called first
                                                     Interlocked.Increment(ref value).Should().BeOneOf(3, 4);
                                                     return returnValue;
                                                 },
                                                 onDelegateAsyncEnd: async (sender, returnValue, exception, state) =>
                                                 {
                                                     // 3 or 4 because we can't predict if onDelegateEnd or onDelegateAsyncEnd will be called first
                                                     Interlocked.Increment(ref value).Should().BeOneOf(3, 4);
                                                     await Task.Delay(100).ConfigureAwait(false);
                                                     return ((int)returnValue) + 1;
                                                 }));
        result = await func2("Arg01").ConfigureAwait(false);
        using var scope = new AssertionScope();
        result.Should().Be(43);
        value.Should().Be(4);
    }

    [Fact]
    public async Task Async1CallbackFailureTest()
    {
        int value = 0;

        CustomFunc<string, Task<int>> func = async (arg1) =>
        {
            Interlocked.Increment(ref value);
            await Task.Delay(100).ConfigureAwait(false);
            return 42;
        };

        func = DelegateInstrumentation.Wrap(func, new DelegateFunc1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 },
                                                 onDelegateAsyncEnd: (sender, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value);
                                                     throw new InvalidOperationException("Expected");
                                                 }));
        var result = await func("Arg01").ConfigureAwait(false);
        using var scope = new AssertionScope();
        value.Should().Be(4);
        result.Should().Be(42);
    }

    [Fact]
    public async Task Async1WithAsyncExceptionTest()
    {
        var result = new StrongBox<int>(0);
        var callbacks = new Async1Callbacks();
        Func<string, Task<int>> func = async (arg1) =>
        {
            callbacks.Count.Value++;
            // force an exception
            int x = 0, y = 0, z = 0;
            z = x / y;
            await Task.Yield();
            return 42;
        };
        func = func.Instrument(callbacks);
        await Assert.ThrowsAsync<DivideByZeroException>(
            async () =>
            {
                result = new StrongBox<int>(await func("Arg01").ConfigureAwait(false));
            }).ConfigureAwait(false);

        callbacks.Count.Value.Should().Be(4);

        var value = 0;
        CustomFunc<string, Task<int>> func2 = async (arg1) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            // force an exception
            int x = 0, y = 0, z = 0;
            z = x / y;
            await Task.Yield();
            return 42;
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(4);
                                                     return returnValue;
                                                 },
                                                 onDelegateAsyncEnd: async (sender, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     await Task.Delay(100).ConfigureAwait(false);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        await Assert.ThrowsAsync<DivideByZeroException>(
            async () =>
            {
                result = new StrongBox<int>(await func2("Arg01").ConfigureAwait(false));
            }).ConfigureAwait(false);

        value.Should().Be(4);
    }

    [Fact]
    public async Task Async1WithExceptionTest()
    {
        var result = new StrongBox<int>(0);
        var callbacks = new Async1Callbacks();
        Func<string, Task<int>> func = (arg1) =>
        {
            callbacks.Count.Value++;
            // force an exception
            int x = 0, y = 0, z = 0;
            z = x / y;
            return Task.FromResult(42);
        };
        func = func.Instrument(callbacks);
        await Assert.ThrowsAsync<DivideByZeroException>(
            async () =>
            {
                result = new StrongBox<int>(await func("Arg01").ConfigureAwait(false));
            }).ConfigureAwait(false);

        callbacks.Count.Value.Should().Be(4);

        var value = 0;
        CustomFunc<string, Task<int>> func2 = (arg1) =>
        {
            Interlocked.Increment(ref value).Should().Be(2);
            // force an exception
            int x = 0, y = 0, z = 0;
            z = x / y;
            return Task.FromResult(42);
        };
        func2 = DelegateInstrumentation.Wrap(func2, new DelegateFunc1Callbacks(
                                                 (target, arg1) =>
                                                 {
                                                     arg1.Should().Be("Arg01");
                                                     Interlocked.Increment(ref value).Should().Be(1);
                                                     return null;
                                                 },
                                                 (target, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(4);
                                                     return returnValue;
                                                 },
                                                 onDelegateAsyncEnd: async (sender, returnValue, exception, state) =>
                                                 {
                                                     Interlocked.Increment(ref value).Should().Be(3);
                                                     await Task.Delay(100).ConfigureAwait(false);
                                                     return ((int)returnValue) + 1;
                                                 }));
        using var scope = new AssertionScope();
        await Assert.ThrowsAsync<DivideByZeroException>(
            async () =>
            {
                result = new StrongBox<int>(await func2("Arg01").ConfigureAwait(false));
            }).ConfigureAwait(false);

        value.Should().Be(4);
    }

    public readonly struct Async1Callbacks : IBegin1Callbacks, IReturnCallback, IReturnAsyncCallback
    {
        public Async1Callbacks()
        {
            Count = new StrongBox<int>(0);
        }

        public StrongBox<int> Count { get; }

        public bool PreserveAsyncContext => false;

        public object OnDelegateBegin<TArg1>(object sender, ref TArg1 arg1)
        {
            arg1.Should().Be("Arg01");
            Count.Value++;
            return null;
        }

        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return returnValue;
        }

        public Task<TInnerReturn> OnDelegateEndAsync<TInnerReturn>(object sender, TInnerReturn returnValue, Exception exception, object state)
        {
            Count.Value++;
            return (Task<TInnerReturn>)(object)Task.FromResult((int)(object)returnValue + 1);
        }

        public void OnException(object sender, Exception ex)
        {
        }
    }
}

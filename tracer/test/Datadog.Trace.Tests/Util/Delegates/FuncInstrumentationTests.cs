// <copyright file="FuncInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

public class FuncInstrumentationTests
{
    [Fact]
    public void Func0Test()
    {
        var value = 0;
        Func<int> func = () =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func0Callbacks
            {
                OnDelegateBegin = target =>
                {
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func().Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func1Test()
    {
        var value = 0;
        Func<string, int> func = (arg1) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func1Callbacks
            {
                OnDelegateBegin = (target, arg1) =>
                {
                    arg1.Should().Be("Arg01");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func("Arg01").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func2Test()
    {
        var value = 0;
        Func<string, string, int> func = (arg1, arg2) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func2Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func("Arg01", "Arg02").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func3Test()
    {
        var value = 0;
        Func<string, string, string, int> func = (arg1, arg2, arg3) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func3Callbacks
            {
                OnDelegateBegin = (target, arg1, arg2, arg3) =>
                {
                    arg1.Should().Be("Arg01");
                    arg2.Should().Be("Arg02");
                    arg3.Should().Be("Arg03");
                    Interlocked.Increment(ref value);
                    return null;
                },
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func("Arg01", "Arg02", "Arg03").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func4Test()
    {
        var value = 0;
        Func<string, string, string, string, int> func = (arg1, arg2, arg3, arg4) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func4Callbacks
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
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func("Arg01", "Arg02", "Arg03", "Arg04").Should().Be(43);
        value.Should().Be(3);
    }

    [Fact]
    public void Func5Test()
    {
        var value = 0;
        Func<string, string, string, string, string, int> func = (arg1, arg2, arg3, arg4, arg5) =>
        {
            Interlocked.Increment(ref value);
            return 42;
        };
        func = FuncInstrumentation.Wrap(
            func,
            new Func5Callbacks
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
                OnDelegateEnd = (target, returnValue, exception, state) =>
                {
                    Interlocked.Increment(ref value);
                    return ((int)returnValue) + 1;
                },
            });

        func("Arg01", "Arg02", "Arg03", "Arg04", "Arg05").Should().Be(43);
        value.Should().Be(3);
    }
}

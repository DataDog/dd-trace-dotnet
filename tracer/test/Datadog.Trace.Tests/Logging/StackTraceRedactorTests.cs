// <copyright file="StackTraceRedactorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Logging.Internal.Sinks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class StackTraceRedactorTests
{
    [Fact]
    public void Redact_IsEmptyForEmptyException()
    {
        var ex = new Exception();
        var stackTrace = new StackTrace(ex);

        var sb = new StringBuilder();
        StackTraceRedactor.Redact(sb, stackTrace);
        var redacted = sb.ToString();

        redacted.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(TestData.ToStringTestData), MemberType = typeof(TestData))]
    public void Redact_ContainsExpectedStrings(StackTrace stackTrace, string expectedToString)
    {
        var sb = new StringBuilder();
        StackTraceRedactor.Redact(sb, stackTrace);
        var redacted = sb.ToString();

        if (expectedToString.Length == 0)
        {
            redacted.Should().BeEmpty();
            return;
        }

        redacted.Should().Contain(expectedToString);
        redacted.Should().EndWith(Environment.NewLine);
        var redactedFrames = redacted.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        var allowedPrefixes = new[] { "Datadog", "Microsoft", "System", "REDACTED" };
        redactedFrames.Should().OnlyContain(x => allowedPrefixes.Any(x.Contains));

        var originalFrames = stackTrace.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        redactedFrames.Length.Should().Be(originalFrames.Length);
        for (var i = 0; i < redactedFrames.Length; i++)
        {
            if (redactedFrames[i].Contains("REDACTED"))
            {
                continue;
            }

            redactedFrames[i].Should().Be(originalFrames[i]);
        }
    }

    [Fact]
    public unsafe void FunctionPointerSignature_ContainsExpectedString()
    {
        // This is separate from Redact_ContainsExpectedStrings since unsafe cannot be used for iterators
        var stackTrace = TestData.FunctionPointerParameter(null);
        Assert.Contains("Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.FunctionPointerParameter(IntPtr x)", stackTrace.ToString());
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private static Exception InvokeException()
    {
        try
        {
            ThrowException();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void ThrowException() => throw new Exception();

    public static class TestData
    {
        public static IEnumerable<object[]> ToStringTestData()
        {
            yield return new object[] { new StackTrace(InvokeException()), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.ThrowException()" };
            yield return new object[] { new StackTrace(new Exception()), string.Empty };
            yield return new object[] { NoParameters(), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.NoParameters()" };
            yield return new object[] { OneParameter(1), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.OneParameter(Int32 x)" };
            yield return new object[] { TwoParameters(1, null), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.TwoParameters(Int32 x, String y)" };
            yield return new object[] { Generic<int>(), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.Generic[T]()" };
            yield return new object[] { Generic<int, string>(), "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.Generic[T1,T2]()" };
            yield return new object[] { new ClassWithConstructor().StackTrace, "Datadog.Trace.Tests.Logging.StackTraceRedactorTests.TestData.ClassWithConstructor..ctor()" };
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        internal static unsafe StackTrace FunctionPointerParameter(delegate*<void> x) => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static StackTrace NoParameters() => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static StackTrace OneParameter(int x) => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static StackTrace TwoParameters(int x, string y) => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static StackTrace Generic<T>() => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static StackTrace Generic<T1, T2>() => new();

        private class ClassWithConstructor
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public ClassWithConstructor() => StackTrace = new StackTrace();

            public StackTrace StackTrace { get; }
        }
    }
}

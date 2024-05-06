// <copyright file="ExceptionRedactorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Logging.Internal;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class ExceptionRedactorTests
{
    [Theory]
    [InlineData(typeof(Exception))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    public void Redact_UsesExceptionTypeForEmptyException(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType);
        var redacted = ExceptionRedactor.Redact(ex);

        // We don't necessarily _want_ the new line at the end, but it's fine
        redacted.Should().Be(exceptionType.FullName + Environment.NewLine);
    }

    [Fact]
    public void Redact_IncludesExceptionTypeWhenHaveStackFrames()
    {
        var ex = InvokeException();
        var redacted = ExceptionRedactor.Redact(ex);

        redacted.Should().StartWith("System.Exception" + Environment.NewLine);

        // remove first line (exception) from redacted exception before comparing
        var redactedStackTrace = string.Join(Environment.NewLine, redacted.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Skip(1));
        HasExpectedFrames(new StackTrace(ex), redactedStackTrace);
    }

    [Fact]
    public void Redact_IncludesInnerExceptionWhenHaveStackFrames()
    {
        var ex = InvokeInnerException();
        var redacted = ExceptionRedactor.Redact(ex);

        redacted.Should().StartWith("System.InvalidOperationException ---> System.Exception" + Environment.NewLine);

        // split on the stack trace separator, and then remove the first line (exception types)
        // from redacted exception before comparing
        var stackTraces = redacted.Split(["   --- End of inner exception stack trace ---"], StringSplitOptions.RemoveEmptyEntries);
        stackTraces.Should().HaveCount(2);
        ex.InnerException.Should().NotBeNull();
        ex.InnerException.InnerException.Should().BeNull();

        // inner stack trace first
        var redactedStackTrace = string.Join(Environment.NewLine, stackTraces[0].Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Skip(1));
        HasExpectedFrames(new StackTrace(ex.InnerException), redactedStackTrace);

        // outer stack trace
        HasExpectedFrames(new StackTrace(ex), stackTraces[1]);
    }

    [Fact]
    public void RedactStackTrace_IsEmptyForEmptyException()
    {
        var ex = new Exception();
        var stackTrace = new StackTrace(ex);

        var sb = new StringBuilder();
        ExceptionRedactor.RedactStackTrace(sb, stackTrace);
        var redacted = sb.ToString();

        redacted.Should().BeEmpty();
    }

    [Fact]
    public void RedactStackTrace_RedactsUserCode()
    {
        foreach (var method in TestData.MethodsToRedact())
        {
            var methodBase = method.Should().NotBeNull().And.BeAssignableTo<MethodBase>().Subject;
            var stackFrame = new TestStackFrame(methodBase);
            var stackTrace = new StackTrace(stackFrame);

            var sb = new StringBuilder();
            ExceptionRedactor.RedactStackTrace(sb, stackTrace);
            var redacted = sb.ToString();

            redacted.Should().Be($"{ExceptionRedactor.StackFrameAt}{ExceptionRedactor.Redacted}" + Environment.NewLine);
        }
    }

    [Fact]
    public void RedactStackTrace_DoesNotRedactBclAndDatadog()
    {
        foreach (var method in TestData.MethodsToNotRedact())
        {
            var methodBase = method.Should().NotBeNull().And.BeAssignableTo<MethodBase>().Subject;
            var stackFrame = new TestStackFrame(methodBase);
            var stackTrace = new StackTrace(stackFrame);

            var sb = new StringBuilder();
            ExceptionRedactor.RedactStackTrace(sb, stackTrace);
            var redacted = sb.ToString();

            redacted.Should().Be(stackTrace.ToString());
        }
    }

    [Fact]
    public void RedactStackTrace_ContainsExpectedStrings()
    {
        foreach (var (stackTrace, expectedToString) in TestData.ToStringTestData())
        {
            var sb = new StringBuilder();
            ExceptionRedactor.RedactStackTrace(sb, stackTrace);
            var redacted = sb.ToString();

            if (expectedToString.Length == 0)
            {
                redacted.Should().BeEmpty();
                return;
            }

            redacted.Should().Contain(expectedToString);
            redacted.Should().EndWith(Environment.NewLine);

            HasExpectedFrames(stackTrace, redacted);
        }
    }

    [Fact]
    public unsafe void RedactStackTrace_WorksWithFunctionPointerSignature()
    {
        // This is separate from Redact_ContainsExpectedStrings since unsafe cannot be used for iterators
        var stackTrace = TestData.FunctionPointerParameter(null);
        var sb = new StringBuilder();
        ExceptionRedactor.RedactStackTrace(sb, stackTrace);
        var redacted = sb.ToString();

#if NET8_0_OR_GREATER
        // https://github.com/dotnet/runtime/issues/11354
        redacted.Should().Contain("Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.FunctionPointerParameter( x)");
#else
        redacted.Should().Contain("Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.FunctionPointerParameter(IntPtr x)");
#endif
        redacted.Should().EndWith(Environment.NewLine);

        HasExpectedFrames(stackTrace, redacted);
    }

    private static void HasExpectedFrames(StackTrace stackTrace, string redacted)
    {
        var redactedFrames = redacted.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        // assumes that all methods in the call stack include one of these in the namespace
        // We actually filter based on assembly name so this isn't guaranteed, but works in tests
        var allowedPrefixes = new[] { "Datadog", "Microsoft", "System", "Xunit", "REDACTED" };
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

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private static Exception InvokeInnerException()
    {
        try
        {
            try
            {
                ThrowException();
            }
            catch (Exception inner)
            {
                ThrowInnerException(inner);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void ThrowException() => throw new Exception();

    private static void ThrowInnerException(Exception inner) => throw new InvalidOperationException(null, inner);

    public static class TestData
    {
        public static List<object> MethodsToRedact() =>
        [
            typeof(AssertionExtensions).GetMethod(nameof(AssertionExtensions.Should), types: new[] { typeof(object) }),
            typeof(Xunit.Assert).GetMethod(nameof(Assert.False), types: new[] { typeof(bool) }),
            typeof(VerifyTests.VerifierSettings).GetMethod(nameof(VerifyTests.VerifierSettings.DisableClipboard)),
            typeof(VerifyTests.VerifierSettings).GetProperty(nameof(VerifyTests.VerifierSettings.StrictJson))?.GetMethod,
            typeof(VerifyTests.VerifierSettings).GetProperty(nameof(VerifyTests.VerifierSettings.StrictJson))?.SetMethod,
            typeof(VerifyTests.SerializationSettings).GetConstructor(Array.Empty<Type>()),
        ];

        public static List<object> MethodsToNotRedact() =>
        [
            typeof(ExceptionRedactorTests.TestData).GetMethod(nameof(NoParameters)),
            typeof(Datadog.Trace.Tracer).GetMethod(nameof(Tracer.UnsafeSetTracerInstance), BindingFlags.Static | BindingFlags.NonPublic),
            typeof(Datadog.Trace.Tracer).GetProperty(nameof(Tracer.Instance))?.GetMethod,
            typeof(Datadog.Trace.Tracer).GetProperty(nameof(Tracer.Instance))?.SetMethod,
            typeof(Datadog.Trace.Vendors.Serilog.Log).GetProperty(nameof(Serilog.Log.Logger))?.GetMethod,
            typeof(Datadog.Trace.Vendors.Serilog.Log).GetProperty(nameof(Serilog.Log.Logger))?.SetMethod,
            typeof(Datadog.Trace.Vendors.Serilog.Log).GetMethod(nameof(Serilog.Log.CloseAndFlush)),
            typeof(StringBuilder).GetMethod(nameof(StringBuilder.Clear)),
            typeof(string).GetMethod(nameof(string.IndexOf),  types: new[] { typeof(char) }),
            typeof(Serilog.Log).GetProperty(nameof(Serilog.Log.Logger))?.GetMethod,
            typeof(Serilog.Log).GetProperty(nameof(Serilog.Log.Logger))?.SetMethod,
            typeof(Serilog.Log).GetMethod(nameof(Serilog.Log.CloseAndFlush)),
            typeof(Serilog.Log).GetMethod(nameof(Serilog.Log.Error), types: new[] { typeof(string) }),
            typeof(System.Threading.Tasks.Task).GetMethod(nameof(System.Threading.Tasks.Task.Wait), types: Array.Empty<Type>()),
            typeof(System.Data.SqlClient.SqlCommand).GetMethod(nameof(System.Data.SqlClient.SqlCommand.Clone)),
            typeof(System.Data.SQLite.SQLiteCommand).GetProperty(nameof(System.Data.SQLite.SQLiteCommand.CommandType))?.GetMethod,
            typeof(System.Data.SQLite.SQLiteCommand).GetProperty(nameof(System.Data.SQLite.SQLiteCommand.CommandType))?.SetMethod,
#if !NETFRAMEWORK
            typeof(Microsoft.CodeAnalysis.DiagnosticDescriptor).GetProperty(nameof(Microsoft.CodeAnalysis.DiagnosticDescriptor.Id))?.GetMethod,
            typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).GetMethod(nameof(Microsoft.Extensions.DependencyInjection.ServiceCollection.Contains), types: new[] { typeof(Microsoft.Extensions.DependencyInjection.ServiceDescriptor) }),
#endif
        ];

        public static List<(StackTrace StackTrace, string ExpectedToString)> ToStringTestData() =>
        [
            (new StackTrace(InvokeException()), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.ThrowException()"),
            (new StackTrace(new Exception()), string.Empty),
            (NoParameters(), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.NoParameters()"),
            (OneParameter(1), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.OneParameter(Int32 x)"),
            (TwoParameters(1, null), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.TwoParameters(Int32 x, String y)"),
            (Generic<int>(), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.Generic[T]()"),
            (Generic<int, string>(), "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.Generic[T1,T2]()"),
            (new ClassWithConstructor().StackTrace, "Datadog.Trace.Tests.Logging.ExceptionRedactorTests.TestData.ClassWithConstructor..ctor()"),
        ];

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static unsafe StackTrace FunctionPointerParameter(delegate*<void> x) => new();

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static StackTrace NoParameters() => new();

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

    public class TestStackFrame : StackFrame
    {
        private readonly MethodBase _methodBase;

        public TestStackFrame(MethodBase methodBase)
        {
            _methodBase = methodBase;
        }

        public override MethodBase GetMethod() => _methodBase;
    }
}

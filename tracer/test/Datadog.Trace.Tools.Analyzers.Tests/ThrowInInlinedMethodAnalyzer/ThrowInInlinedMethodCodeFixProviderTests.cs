// <copyright file="ThrowInInlinedMethodCodeFixProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

#nullable enable

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer.ThrowInInlinedMethodAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer.ThrowInInlinedMethodCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ThrowInInlinedMethodAnalyzer;

public class ThrowInInlinedMethodCodeFixProviderTests
{
    private const string DiagnosticId = Analyzers.ThrowInInlinedMethodAnalyzer.Diagnostics.DiagnosticId;

    // Minimal ThrowHelper stub so the fixed code compiles in the test harness
    private const string ThrowHelperStub = """

        namespace Datadog.Trace.Util
        {
            internal static class ThrowHelper
            {
                internal static void ThrowArgumentNullException(string paramName) => throw new System.ArgumentNullException(paramName);
                internal static void ThrowArgumentOutOfRangeException(string paramName) => throw new System.ArgumentOutOfRangeException(paramName);
                internal static void ThrowArgumentOutOfRangeException(string paramName, string message) => throw new System.ArgumentOutOfRangeException(paramName, message);
                internal static void ThrowArgumentOutOfRangeException(string paramName, object actualValue, string message) => throw new System.ArgumentOutOfRangeException(paramName, actualValue, message);
                internal static void ThrowArgumentException(string message) => throw new System.ArgumentException(message);
                internal static void ThrowArgumentException(string message, string paramName) => throw new System.ArgumentException(message, paramName);
                internal static void ThrowInvalidOperationException(string message) => throw new System.InvalidOperationException(message);
                internal static void ThrowException(string message) => throw new System.Exception(message);
                internal static void ThrowInvalidCastException(string message) => throw new System.InvalidCastException(message);
                internal static void ThrowIndexOutOfRangeException(string message) => throw new System.IndexOutOfRangeException(message);
                internal static void ThrowNotSupportedException(string message) => throw new System.NotSupportedException(message);
                internal static void ThrowKeyNotFoundException(string message) => throw new System.Collections.Generic.KeyNotFoundException(message);
                internal static void ThrowNullReferenceException(string message) => throw new System.NullReferenceException(message);
            }
        }
        """;

    [Fact]
    public async Task ShouldFixThrowArgumentNullException()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg)
                {
                    if (arg is null)
                    {
                        {|#0:throw new ArgumentNullException(nameof(arg));|}
                    }
                }
            }
            """;

        const string fix = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg)
                {
                    if (arg is null)
                    {
                        ThrowHelper.ThrowArgumentNullException(nameof(arg));
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyCodeFixAsync(source + ThrowHelperStub, expected, fix + ThrowHelperStub);
    }

    [Fact]
    public async Task ShouldFixThrowInvalidOperationException()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    {|#0:throw new InvalidOperationException("something went wrong");|}
                }
            }
            """;

        const string fix = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    ThrowHelper.ThrowInvalidOperationException("something went wrong");
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyCodeFixAsync(source + ThrowHelperStub, expected, fix + ThrowHelperStub);
    }

    [Fact]
    public async Task ShouldFixThrowArgumentOutOfRangeExceptionWithThreeArgs()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(int value)
                {
                    if (value < 0)
                    {
                        {|#0:throw new ArgumentOutOfRangeException(nameof(value), value, "must be non-negative");|}
                    }
                }
            }
            """;

        const string fix = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(int value)
                {
                    if (value < 0)
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), value, "must be non-negative");
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyCodeFixAsync(source + ThrowHelperStub, expected, fix + ThrowHelperStub);
    }

    [Fact]
    public async Task ShouldFixMultipleThrowsInSameMethod()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg1, object? arg2)
                {
                    if (arg1 is null)
                    {
                        {|#0:throw new ArgumentNullException(nameof(arg1));|}
                    }

                    if (arg2 is null)
                    {
                        {|#1:throw new ArgumentNullException(nameof(arg2));|}
                    }
                }
            }
            """;

        const string fix = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg1, object? arg2)
                {
                    if (arg1 is null)
                    {
                        ThrowHelper.ThrowArgumentNullException(nameof(arg1));
                    }

                    if (arg2 is null)
                    {
                        ThrowHelper.ThrowArgumentNullException(nameof(arg2));
                    }
                }
            }
            """;

        var expected = new[]
        {
            new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("TestMethod"),
            new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("TestMethod"),
        };

        await Verifier.VerifyCodeFixAsync(source + ThrowHelperStub, expected, fix + ThrowHelperStub);
    }

    [Fact]
    public async Task ShouldNotFixUnsupportedExceptionType()
    {
        // NotImplementedException is not in ThrowHelper, so no fix should be offered.
        // The diagnostic is still reported, but the verifier accepts no code fix.
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    {|#0:throw new NotImplementedException();|}
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");

        // Verify the diagnostic is reported but the source is unchanged (no fix applied)
        await Verifier.VerifyCodeFixAsync(source, expected, source);
    }

    [Fact]
    public async Task ShouldNotFixZeroArgConstructor()
    {
        // throw new InvalidOperationException() has 0 args, ThrowHelper requires 1
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    {|#0:throw new InvalidOperationException();|}
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");

        await Verifier.VerifyCodeFixAsync(source, expected, source);
    }

    [Fact]
    public async Task ShouldNotFixThrowExpression()
    {
        // Throw expressions can't be replaced with void ThrowHelper calls
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                object TestMethod(object? arg)
                {
                    return arg ?? {|#0:throw new ArgumentNullException(nameof(arg))|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");

        await Verifier.VerifyCodeFixAsync(source, expected, source);
    }

    [Fact]
    public async Task ShouldNotFixBareThrow()
    {
        // bare throw; (rethrow) has no exception constructor to map
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    try
                    {
                        Console.WriteLine();
                    }
                    catch
                    {
                        {|#0:throw;|}
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");

        await Verifier.VerifyCodeFixAsync(source, expected, source);
    }

    [Fact]
    public async Task ShouldNotAddDuplicateUsingDirective()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg)
                {
                    if (arg is null)
                    {
                        {|#0:throw new ArgumentNullException(nameof(arg));|}
                    }
                }
            }
            """;

        const string fix = """
            using System;
            using System.Runtime.CompilerServices;
            using Datadog.Trace.Util;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod(object? arg)
                {
                    if (arg is null)
                    {
                        ThrowHelper.ThrowArgumentNullException(nameof(arg));
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyCodeFixAsync(source + ThrowHelperStub, expected, fix + ThrowHelperStub);
    }
}

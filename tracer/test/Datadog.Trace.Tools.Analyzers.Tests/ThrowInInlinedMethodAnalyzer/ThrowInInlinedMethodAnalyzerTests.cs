// <copyright file="ThrowInInlinedMethodAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer.ThrowInInlinedMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ThrowInInlinedMethodAnalyzer;

public class ThrowInInlinedMethodAnalyzerTests
{
    private const string DiagnosticId = Analyzers.ThrowInInlinedMethodAnalyzer.Diagnostics.DiagnosticId;

    [Fact]
    public async Task EmptySourceShouldNotHaveDiagnostics()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task ShouldNotFlagThrowInRegularMethod()
    {
        const string source = """
            using System;

            class TestClass
            {
                void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ShouldNotFlagThrowInNoInliningMethod()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                void TestMethod()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ShouldNotFlagAggressiveInliningMethodWithoutThrow()
    {
        const string source = """
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                int Add(int a, int b) => a + b;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ShouldNotFlagThrowInLambdaInsideAggressiveInliningMethod()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void TestMethod()
                {
                    Action a = () => throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ShouldFlagThrowNewInAggressiveInliningMethod()
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

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagThrowExpressionInAggressiveInliningMethod()
    {
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
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagMultipleThrowsInAggressiveInliningMethod()
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

        var expected1 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        var expected2 = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(1)
            .WithArguments("TestMethod");
        await Verifier.VerifyAnalyzerAsync(source, expected1, expected2);
    }

    [Fact]
    public async Task ShouldFlagAggressiveInliningWithCombinedFlags()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.NoOptimization)]
                void TestMethod()
                {
                    {|#0:throw new InvalidOperationException();|}
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestMethod");
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagThrowInAggressiveInliningConstructor()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public TestClass(object? arg)
                {
                    if (arg is null)
                    {
                        {|#0:throw new ArgumentNullException(nameof(arg));|}
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("TestClass");
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagRethrowInAggressiveInliningMethod()
    {
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
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagThrowInAggressiveInliningLocalFunction()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                void OuterMethod()
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    int LocalFunc(int value)
                    {
                        if (value < 0)
                        {
                            {|#0:throw new ArgumentOutOfRangeException(nameof(value));|}
                        }

                        return value;
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("LocalFunc");
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ShouldFlagThrowInAggressiveInliningPropertyGetter()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;

            class TestClass
            {
                private object? _value;

                object Value
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        {|#0:throw new InvalidOperationException();|}
                    }
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("get");
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }
}

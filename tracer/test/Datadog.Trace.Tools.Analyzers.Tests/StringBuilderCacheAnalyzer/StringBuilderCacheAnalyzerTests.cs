// <copyright file="StringBuilderCacheAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer.StringBuilderCacheAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.StringBuilderCacheAnalyzer;

public class StringBuilderCacheAnalyzerTests
{
    private const string StringBuilderCacheStub = """
        namespace Datadog.Trace.Util
        {
            internal static class StringBuilderCache
            {
                internal const int MaxBuilderSize = 360;
                public static System.Text.StringBuilder Acquire(int capacity = 360) => new System.Text.StringBuilder(capacity);
                public static string GetStringAndRelease(System.Text.StringBuilder sb) => sb.ToString();
                public static void Release(System.Text.StringBuilder sb) { }
            }
        }
        """;

    [Fact]
    public async Task EmptySource_NoDiagnostic()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task NewStringBuilder_NoArgs_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_WithCapacity_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder(100)|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_LargeCapacity_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder(500)|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_WithString_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder("hello")|};
                    sb.Append(" world");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_WithStringAndCapacity_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder("hello", 10)|};
                    sb.Append(" world");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MethodAlreadyUsesStringBuilderCache_NoDiagnostic()
    {
        var source = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);

                    // This new StringBuilder should be suppressed because the method already uses StringBuilderCache
                    var sb2 = new StringBuilder();
                    sb2.Append("world");
                    var result2 = sb2.ToString();
                }
            }
            """ + StringBuilderCacheStub;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task InsideStringBuilderCacheClass_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class StringBuilderCache
            {
                public static StringBuilder Acquire(int capacity = 360)
                {
                    return new StringBuilder(capacity);
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_InLambda_WhenOuterMethodUsesCache_ReportsDiagnostic()
    {
        // The lambda is a separate scope — the outer method's StringBuilderCache.Acquire() doesn't suppress it
        var source = """
            using System;
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);

                    Action action = () =>
                    {
                        var sb2 = {|#0:new StringBuilder()|};
                        sb2.Append("world");
                    };
                }
            }
            """ + StringBuilderCacheStub;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_InLocalFunction_WhenOuterMethodUsesCache_ReportsDiagnostic()
    {
        var source = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);

                    void LocalFunction()
                    {
                        var sb2 = {|#0:new StringBuilder()|};
                        sb2.Append("world");
                    }
                }
            }
            """ + StringBuilderCacheStub;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_FieldInitializer_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                private readonly StringBuilder _sb = new StringBuilder();

                void TestMethod()
                {
                    _sb.Clear();
                    _sb.Append("hello");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_FieldInitializerWithCapacity_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                private readonly StringBuilder _sb = new StringBuilder(1024);

                void TestMethod()
                {
                    _sb.Clear();
                    _sb.Append("hello");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_ConstructorAssignmentToField_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                private readonly StringBuilder _sb;

                TestClass()
                {
                    _sb = new StringBuilder(256);
                }

                void TestMethod()
                {
                    _sb.Clear();
                    _sb.Append("hello");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_PropertyInitializer_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                private StringBuilder Sb { get; } = new StringBuilder();

                void TestMethod()
                {
                    Sb.Clear();
                    Sb.Append("hello");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_VariableCapacity_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod(int len)
                {
                    var sb = {|#0:new StringBuilder(len)|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleInSameMethod_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb1 = new StringBuilder();
                    var sb2 = new StringBuilder();
                    sb1.Append("hello");
                    sb2.Append("world");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleInDifferentMethods_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void Method1()
                {
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                }

                void Method2()
                {
                    var sb = {|#1:new StringBuilder()|};
                    sb.Append("world");
                }
            }
            """;

        var expected = new[]
        {
            Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0),
            Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(1),
        };
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleButOneInLambda_ReportsDiagnostic()
    {
        // Each scope has only one StringBuilder, so both should be flagged
        var source = """
            using System;
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");

                    Action action = () =>
                    {
                        var sb2 = {|#1:new StringBuilder()|};
                        sb2.Append("world");
                    };
                }
            }
            """;

        var expected = new[]
        {
            Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0),
            Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(1),
        };
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }
}

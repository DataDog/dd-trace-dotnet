// <copyright file="StringBuilderCacheAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
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

    // ── Constructor variants that should report a diagnostic ──────────────

    public static IEnumerable<object[]> ConstructorVariants_ReportDiagnostic => new[]
    {
        new object[]
        {
            "no args",
            """
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
            """,
        },
        new object[]
        {
            "capacity within MaxBuilderSize",
            """
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
            """,
        },
        new object[]
        {
            "string arg",
            """
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
            """,
        },
        new object[]
        {
            "string and capacity args",
            """
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
            """,
        },
        new object[]
        {
            "variable capacity (non-constant)",
            """
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
            """,
        },
        new object[]
        {
            "implicit new() no args",
            """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    StringBuilder sb = {|#0:new()|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """,
        },
        new object[]
        {
            "implicit new() with capacity",
            """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    StringBuilder sb = {|#0:new(100)|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """,
        },
    };

    // ── Field/property assignment — suppressed ───────────────────────────

    public static IEnumerable<object[]> FieldOrPropertyAssignment_NoDiagnostic => new[]
    {
        new object[]
        {
            "field initializer",
            """
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
            """,
        },
        new object[]
        {
            "field initializer with capacity",
            """
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
            """,
        },
        new object[]
        {
            "constructor assignment to field",
            """
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
            """,
        },
        new object[]
        {
            "property initializer",
            """
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
            """,
        },
    };

    // ── Test methods ─────────────────────────────────────────────────────

    [Fact]
    public async Task EmptySource_NoDiagnostic()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Theory]
    [MemberData(nameof(ConstructorVariants_ReportDiagnostic))]
    public async Task NewStringBuilder_VariousConstructors_ReportsDiagnostic(string description, string source)
    {
        _ = description; // used for test display only
        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    // ── Capacity exceeds MaxBuilderSize — suppressed ─────────────────────

    [Theory]
    [InlineData(361)]
    [InlineData(500)]
    [InlineData(1024)]
    public async Task NewStringBuilder_ConstantCapacityExceedsMaxBuilderSize_NoDiagnostic(int capacity)
    {
        var source = $$"""
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = new StringBuilder({{capacity}});
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_CapacityExactlyAtMaxBuilderSize_ReportsDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder(360)|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_StringAndLargeCapacity_NoDiagnostic()
    {
        // StringBuilder(string, int capacity) where capacity > MaxBuilderSize
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = new StringBuilder("hello", 500);
                    sb.Append(" world");
                    var result = sb.ToString();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_FourArgConstructor_LargeCapacity_NoDiagnostic()
    {
        // StringBuilder(string, int startIndex, int length, int capacity) where capacity > MaxBuilderSize
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = new StringBuilder("hello world", 0, 5, 500);
                    sb.Append(" more");
                    var result = sb.ToString();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_CapacityAndMaxCapacity_LargeCapacity_NoDiagnostic()
    {
        // StringBuilder(int capacity, int maxCapacity) where capacity > MaxBuilderSize
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = new StringBuilder(500, 1000);
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    // ── StringBuilderCache already in use — suppressed ───────────────────

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
    public async Task MethodAlreadyUsesQualifiedStringBuilderCache_NoDiagnostic()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = Datadog.Trace.Util.StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = Datadog.Trace.Util.StringBuilderCache.GetStringAndRelease(sb);

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

    // ── Scope isolation (lambdas, local functions) ───────────────────────

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

    [Fact]
    public async Task NewStringBuilder_InNestedLambda_WhenOuterHasMultiple_ReportsDiagnostic()
    {
        // Outer method has 2 StringBuilders (suppressed), but nested lambda has just 1 (should be flagged)
        var source = """
            using System;
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb1 = new StringBuilder();
                    var sb2 = new StringBuilder();
                    sb1.Append("hello");
                    sb2.Append("world");

                    Action action = () =>
                    {
                        var sb3 = {|#0:new StringBuilder()|};
                        sb3.Append("nested");
                    };
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleInLocalFunction_SuppressedCorrectly()
    {
        // Two StringBuilders in the same local function scope should suppress
        // the diagnostic, just like they do in a regular method.
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    void LocalFunction()
                    {
                        var sb1 = new StringBuilder();
                        var sb2 = new StringBuilder();
                        sb1.Append("hello");
                        sb2.Append("world");
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NewStringBuilder_LocalFunctionWithCacheCall_SuppressedCorrectly()
    {
        // A local function that already calls StringBuilderCache.Acquire()
        // should suppress the diagnostic for additional StringBuilder allocations in the same scope.
        var source = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    void LocalFunction()
                    {
                        var sb = StringBuilderCache.Acquire();
                        sb.Append("hello");
                        var result = StringBuilderCache.GetStringAndRelease(sb);

                        var sb2 = new StringBuilder();
                        sb2.Append("world");
                    }
                }
            }
            """ + StringBuilderCacheStub;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    // ── Multiple allocations in same scope — suppressed ──────────────────

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
    public async Task NewStringBuilder_MultipleInSameMethod_WithFieldAssignment_ReportsDiagnostic()
    {
        // Only one StringBuilder is method-scoped (the field assignment doesn't count),
        // so the method-scoped one should be flagged
        var source = """
            using System.Text;

            class TestClass
            {
                private StringBuilder _sb;

                void TestMethod()
                {
                    _sb = new StringBuilder(256);
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyAnalyzerAsync(source, expected);
    }
}

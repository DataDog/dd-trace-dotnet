// <copyright file="StringBuilderCacheCodeFixTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer.StringBuilderCacheAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer.StringBuilderCacheCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.StringBuilderCacheAnalyzer;

public class StringBuilderCacheCodeFixTests
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
    public async Task NewStringBuilder_NoArgs_ReplacesWithAcquireAndGetStringAndRelease()
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

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_WithCapacity_ForwardsArgumentToAcquire()
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

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire(100);
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleToStringCalls_WithMutations_OnlyReplacesLast()
    {
        // Case B: mutations exist between .ToString() calls — only the last becomes GetStringAndRelease()
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                    var first = sb.ToString();
                    sb.Append(" world");
                    var second = sb.ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var first = sb.ToString();
                    sb.Append(" world");
                    var second = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_MultipleToStringCalls_NoMutations_UsesLocalVariable()
    {
        // Case A: no mutations between .ToString() calls — use single GetStringAndRelease() + local variable
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                    var first = sb.ToString();
                    var second = sb.ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var sbResult = StringBuilderCache.GetStringAndRelease(sb);
                    var first = sbResult;
                    var second = sbResult;
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_ExistingUsingDirective_DoesNotDuplicate()
    {
        var source = """
            using System.Text;
            using Datadog.Trace.Util;

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

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_AssignedViaAssignment_ReplacesToString()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    StringBuilder sb;
                    sb = {|#0:new StringBuilder()|};
                    sb.Append("hello");
                    var result = sb.ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    StringBuilder sb;
                    sb = StringBuilderCache.Acquire();
                    sb.Append("hello");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_InlineUsage_ReplacesToStringWithGetStringAndRelease()
    {
        // Issue 3: When new StringBuilder() is used inline (not assigned to a variable),
        // the code fix should still rewrite the full expression including .ToString().
        var source = """
            using System.Text;

            class TestClass
            {
                string TestMethod()
                {
                    return {|#0:new StringBuilder()|}.Append("hello").ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                string TestMethod()
                {
                    return StringBuilderCache.GetStringAndRelease(StringBuilderCache.Acquire().Append("hello"));
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_WithStringArg_ReplacesWithAcquireAndAppend()
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

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire().Append("hello");
                    sb.Append(" world");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }

    [Fact]
    public async Task NewStringBuilder_WithStringAndCapacityArgs_ReplacesWithAcquireCapacityAndAppend()
    {
        var source = """
            using System.Text;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = {|#0:new StringBuilder("hello", 100)|};
                    sb.Append(" world");
                    var result = sb.ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Text;
            using Datadog.Trace.Util;

            class TestClass
            {
                void TestMethod()
                {
                    var sb = StringBuilderCache.Acquire(100).Append("hello");
                    sb.Append(" world");
                    var result = StringBuilderCache.GetStringAndRelease(sb);
                }
            }
            """;

        var expected = Verifier.Diagnostic(Diagnostics.DiagnosticId).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source + StringBuilderCacheStub, expected, fixedSource + StringBuilderCacheStub);
    }
}

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
    public async Task NewStringBuilder_MultipleToStringCalls_ReplacesAll()
    {
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
                    var first = StringBuilderCache.GetStringAndRelease(sb);
                    sb.Append(" world");
                    var second = StringBuilderCache.GetStringAndRelease(sb);
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
}

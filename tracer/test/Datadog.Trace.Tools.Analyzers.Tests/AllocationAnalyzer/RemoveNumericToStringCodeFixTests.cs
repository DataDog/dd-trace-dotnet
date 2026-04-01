// <copyright file="RemoveNumericToStringCodeFixTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AllocationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.AllocationAnalyzer.NumericToStringInLogAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.AllocationAnalyzer.RemoveNumericToStringCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.AllocationAnalyzer;

public class RemoveNumericToStringCodeFixTests
{
    private const string DiagnosticId = Diagnostics.NumericToStringInLogDiagnosticId;
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_BasicRemoval(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}("Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var fix = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}<int>("Count is {Count}", count);
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_ExplicitSingleGeneric(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}<string>("Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var fix = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}<int>("Count is {Count}", count);
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_ExplicitMultiGeneric_SecondArg(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    string name = "test";
                    int count = 42;
                    Log.{{logMethod}}<string, string>("{Name} has {Count}", name, {|#0:count.ToString()|});
                }
            }
            """;

        var fix = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    string name = "test";
                    int count = 42;
                    Log.{{logMethod}}<string, int>("{Name} has {Count}", name, count);
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_ParenthesizedExpression(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int x = 0;
                    Log.{{logMethod}}<string>("Attempt {Attempt}", {|#0:(x + 1).ToString()|});
                }
            }
            """;

        var fix = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int x = 0;
                    Log.{{logMethod}}<int>("Attempt {Attempt}", (x + 1));
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_ExceptionOverloadWithExplicitGeneric(string logMethod)
    {
        var src = $$"""
            using System;
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Exception ex = null;
                    Log.{{logMethod}}<string>(ex, "Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var fix = $$"""
            using System;
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Exception ex = null;
                    Log.{{logMethod}}<int>(ex, "Count is {Count}", count);
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFix_LongType(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    long size = 123L;
                    Log.{{logMethod}}("Size is {Size}", {|#0:size.ToString()|});
                }
            }
            """;

        var fix = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    long size = 123L;
                    Log.{{logMethod}}<long>("Size is {Size}", size);
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }
}

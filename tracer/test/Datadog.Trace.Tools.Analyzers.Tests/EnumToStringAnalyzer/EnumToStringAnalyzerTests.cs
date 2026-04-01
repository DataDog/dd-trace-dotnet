// <copyright file="EnumToStringAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.EnumToStringAnalyzer.EnumToStringAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using CodeFixVerifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.EnumToStringAnalyzer.EnumToStringAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.EnumToStringAnalyzer.EnumToStringCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.EnumToStringAnalyzer;

public class EnumToStringAnalyzerTests
{
    private const string DiagnosticId = Analyzers.EnumToStringAnalyzer.Diagnostics.DiagnosticId;

    [Fact]
    public async Task EmptySource_NoDiagnostic()
    {
        await AnalyzerVerifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task EnumVariable_ToString_Flags()
    {
        var code = WrapInClass(
            """
            MyEnum value = MyEnum.First;
            var s = {|#0:value.ToString()|};
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithMessage("Calling ToString() on enum 'MyEnum' boxes the value and allocates via reflection; use a ToStringFast() extension (via [EnumExtensions] source generator), a switch expression, or a cached lookup instead");

        await AnalyzerVerifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task EnumLiteral_ToString_Flags()
    {
        var code = WrapInClass(
            """
            var s = {|#0:MyEnum.First.ToString()|};
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task EnumMethodReturn_ToString_Flags()
    {
        var statements = """
            var s = {|#0:GetEnum().ToString()|};
            """;

        var extraMembers = """
            private static MyEnum GetEnum() => MyEnum.First;
            """;

        var code = WrapInClassWithMethod(statements, extraMembers);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task EnumCast_ToString_Flags()
    {
        var code = WrapInClass(
            """
            int x = 0;
            var s = {|#0:((MyEnum)x).ToString()|};
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task StringVariable_ToString_NoDiagnostic()
    {
        var code = WrapInClass(
            """
            string value = "hello";
            var s = value.ToString();
            """);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task IntVariable_ToString_NoDiagnostic()
    {
        var code = WrapInClass(
            """
            int value = 42;
            var s = value.ToString();
            """);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task EnumToStringWithFormatArg_NoDiagnostic()
    {
        var code = WrapInClass(
            """
            MyEnum value = MyEnum.First;
            var s = value.ToString("D");
            """);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task ObjectVariable_ToString_NoDiagnostic()
    {
        var code = WrapInClass(
            """
            object value = new object();
            var s = value.ToString();
            """);

        await AnalyzerVerifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task CodeFix_ReplacesToStringWithToStringFast()
    {
        var code = WrapInClassWithExtension(
            """
            MyEnum value = MyEnum.First;
            var s = {|#0:value.ToString()|};
            """);

        var fix = WrapInClassWithExtension(
            """
            MyEnum value = MyEnum.First;
            var s = value.ToStringFast();
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await CodeFixVerifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task CodeFix_EnumLiteral_ReplacesToStringWithToStringFast()
    {
        var code = WrapInClassWithExtension(
            """
            var s = {|#0:MyEnum.First.ToString()|};
            """);

        var fix = WrapInClassWithExtension(
            """
            var s = MyEnum.First.ToStringFast();
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await CodeFixVerifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task CodeFix_NotOfferedWithoutExtensionMethod()
    {
        // When no ToStringFast extension exists, the diagnostic fires but no code fix is offered.
        // VerifyCodeFixAsync with identical source/fix verifies no changes are made.
        var code = WrapInClass(
            """
            MyEnum value = MyEnum.First;
            var s = {|#0:value.ToString()|};
            """);

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await CodeFixVerifier.VerifyCodeFixAsync(code, expected, code);
    }

    private static string WrapInClass(string statements)
    {
        return $$"""
            enum MyEnum { First, Second, Third }

            class TestClass
            {
                void TestMethod()
                {
                    {{statements}}
                }
            }
            """;
    }

    private static string WrapInClassWithMethod(string statements, string extraMembers)
    {
        return $$"""
            enum MyEnum { First, Second, Third }

            class TestClass
            {
                void TestMethod()
                {
                    {{statements}}
                }

                {{extraMembers}}
            }
            """;
    }

    private static string WrapInClassWithExtension(string statements)
    {
        return $$"""
            enum MyEnum { First, Second, Third }

            static class MyEnumExtensions
            {
                public static string ToStringFast(this MyEnum value)
                    => value switch
                    {
                        MyEnum.First => nameof(MyEnum.First),
                        MyEnum.Second => nameof(MyEnum.Second),
                        MyEnum.Third => nameof(MyEnum.Third),
                        _ => "Unknown",
                    };
            }

            class TestClass
            {
                void TestMethod()
                {
                    {{statements}}
                }
            }
            """;
    }
}

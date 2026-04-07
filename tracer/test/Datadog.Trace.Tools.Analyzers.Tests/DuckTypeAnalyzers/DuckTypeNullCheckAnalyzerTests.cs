// <copyright file="DuckTypeNullCheckAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer.DuckTypeNullCheckAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer.DuckTypeNullCheckCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.DuckTypeAnalyzers;

public class DuckTypeNullCheckAnalyzerTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;
    private const string DiagnosticId = DuckDiagnostics.DuckTypeNullCheckDiagnosticId;

    private static string DuckTypeDefinitions { get; } = """
        #nullable enable

        namespace Datadog.Trace.DuckTyping
        {
            public interface IDuckType
            {
                object Instance { get; }
            }
        }

        namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Foo
        {
            using Datadog.Trace.DuckTyping;
            
            public interface IFoo : IDuckType
            {
                string? Bar { get; }
            }
        }
        """;

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("is")]
    [InlineData("is not")]
    public async Task ShouldNotFlag_Correct_IDuckTypeInstanceNullCheck(string operand)
    {
        var source = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if (duckType.Instance {{operand}} null)
                {
                }
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(source); // no diagnostics expected
    }

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("is")]
    [InlineData("is not")]
    public async Task ShouldFlag_Incorrect_IDuckTypeNullCheck(string operand)
    {
        var source = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if ({|#0:duckType {{operand}} null|})
                {
                }
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);

        var fixedSource = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if (duckType?.Instance {{operand}} null)
                {
                }
            }
        }
        """;

        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("is")]
    [InlineData("is not")]
    public async Task ShouldFlag_EqualsNullCheck_WithNullable(string operand)
    {
        var source = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType? duckType)
            {
                if ({|#0:duckType {{operand}} null|})
                {
                }
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);

        var fixedSource = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType? duckType)
            {
                if (duckType?.Instance {{operand}} null)
                {
                }
            }
        }
        """;

        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    // NOTE: Not doing Theories here because I don't think it is worth it TBH
    [Fact]
    public async Task ShouldFlag_EqualsNullCheck_ExplicitCast()
    {
        var source = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if ({|#0:(object)duckType == null|})
                {
                }
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);

        var fixedSource = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if (duckType?.Instance == null)
                {
                }
            }
        }
        """;

        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task ShouldFlag_EqualsNullCheck_ExplicitCast_And_Parens()
    {
        var source = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if ({|#0:((object)duckType) == null|})
                {
                }
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);

        var fixedSource = $$"""
        using Datadog.Trace.DuckTyping;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            void TestMethod(IDuckType duckType)
            {
                if (duckType?.Instance == null)
                {
                }
            }
        }
        """;

        await Verifier.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [Fact]
    public async Task ShouldFlag_Constraint()
    {
        var code = $$"""
        using Datadog.Trace.DuckTyping;
        using Datadog.Trace.ClrProfiler.AutoInstrumentation.Foo;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            internal static TReturn? OnMethodEnd<TReturn>(TReturn? returnValue)
                where TReturn : IFoo
            {
                if ({|#0:returnValue is not null|})
                {
                    var bar = returnValue.Bar;
                }
        
                return returnValue; // Dummy return
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);

        var fix = $$"""
        using Datadog.Trace.DuckTyping;
        using Datadog.Trace.ClrProfiler.AutoInstrumentation.Foo;

        {{DuckTypeDefinitions}}

        class TestClass
        {
            internal static TReturn? OnMethodEnd<TReturn>(TReturn? returnValue)
                where TReturn : IFoo
            {
                if (returnValue?.Instance is not null)
                {
                    var bar = returnValue.Bar;
                }
        
                return returnValue; // Dummy return
            }
        }
        """;

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }
}

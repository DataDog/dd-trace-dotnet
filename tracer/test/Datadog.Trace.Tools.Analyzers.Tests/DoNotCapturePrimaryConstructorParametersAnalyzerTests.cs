// <copyright file="DoNotCapturePrimaryConstructorParametersAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.PrimaryConstructorAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.PrimaryConstructorAnalyzer.DoNotCapturePrimaryConstructorParametersAnalyzer>;

namespace Datadog.Trace.Tools.Analyzers.Tests;

public class DoNotCapturePrimaryConstructorParametersAnalyzerTests
{
    private const string DiagnosticId = DoNotCapturePrimaryConstructorParametersAnalyzer.DiagnosticId;

    [Fact]
    public async Task ErrorOnCapture_InMethod()
    {
        var source = """
                     class C(int i)
                     {
                         private int M() => [|i|];
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ErrorOnCapture_InProperty()
    {
        var source = """
                     class C(int i)
                     {
                         private int P
                         {
                             get => [|i|];
                             set => [|i|] = value;
                         }
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ErrorOnCapture_InIndexer()
    {
        var source = """
                     class C(int i)
                     {
                         private int this[int param]
                         {
                             get => [|i|];
                             set => [|i|] = value;
                         }
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ErrorOnCapture_InEvent()
    {
        var source = """
                     class C(int i)
                     {
                         public event System.Action E
                         {
                             add => _ = [|i|];
                             remove => _ = [|i|];
                         }
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ErrorOnCapture_UseInSubsequentConstructor()
    {
        var source = """
                     class C(int i)
                     {
                         C(bool b) : this(1)
                         {
                             _ = i;
                         }
                     }
                     """;
        var primaryCtorDiagnostic = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
                                   .WithSpan(5, 13, 5, 14)
                                   .WithArguments("i");
        var compilerError = DiagnosticResult.CompilerError("CS9105")
                                            .WithSpan(5, 13, 5, 14)
                                            .WithArguments("int i");

        await Verifier.VerifyAnalyzerAsync(source, primaryCtorDiagnostic, compilerError);
    }

    [Fact]
    public async Task NoError_PassToBase()
    {
        var source = """
                     class Base(int i);
                     class Derived(int i) : Base(i);
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_FieldInitializer()
    {
        var source = """
                     class C(int i)
                     {
                         public int I = i;
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_PropertyInitializer()
    {
        var source = """
                     class C(int i)
                     {
                         public int I { get; set; } = i;
                     }
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_CapturedInLambda()
    {
        var source = """
                     using System;
                     public class Base(Action action);
                     public class Derived(int i) : Base(() => Console.WriteLine(i));
                     """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoError_LocalFunctionParameterReference()
    {
        var source = """
                     using System;
                     class C
                     {
                        void M()
                        {
                            Nested1(1);
                            void Nested1(int i)
                            {
                                Nested2();
                                void Nested2() => Console.WriteLine(i);
                            }
                        }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }
}

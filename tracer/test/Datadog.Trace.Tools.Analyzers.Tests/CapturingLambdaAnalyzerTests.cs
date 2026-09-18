// <copyright file="CapturingLambdaAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.CapturingLambdaAnalyzer.CapturingLambdaAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests;

public class CapturingLambdaAnalyzerTests
{
    private const string DiagnosticId = CapturingLambdaAnalyzer.Diagnostics.DiagnosticId;

    // === Positive cases: should trigger diagnostic ===

    [Fact]
    public async Task TaskRun_WithCapturingLambda_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             Task.Run({|#0:() => Console.WriteLine(x)|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("Run", "x");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TaskFactoryStartNew_WithCapturingLambda_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             Task.Factory.StartNew({|#0:() => Console.WriteLine(x)|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("StartNew", "x");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ContinueWith_WithCapturingLambda_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             var task = Task.CompletedTask;
                             task.ContinueWith({|#0:t => Console.WriteLine(x)|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("ContinueWith", "x");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ContinueWith_OnGenericTask_WithCapturingLambda_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             var task = Task.FromResult(42);
                             task.ContinueWith({|#0:t => Console.WriteLine(x)|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("ContinueWith", "x");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TaskRun_WithAnonymousDelegate_WithCapture_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             Task.Run({|#0:delegate { Console.WriteLine(x); }|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("Run", "x");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TaskRun_WithMultipleCaptures_ReportsAllVariables()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             int x = 1;
                             string y = "hello";
                             Task.Run({|#0:() =>
                             {
                                 Console.WriteLine(x);
                                 Console.WriteLine(y);
                             }|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("Run", "x, y");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TaskRun_CapturingThis_ReportsDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         private int _field;

                         void M()
                         {
                             Task.Run({|#0:() => Console.WriteLine(_field)|});
                         }
                     }
                     """;

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("Run", "this");

        await Verifier.VerifyAnalyzerAsync(source, expected);
    }

    // === Negative cases: should NOT trigger diagnostic ===

    [Fact]
    public async Task TaskRun_WithStaticLambda_NoDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             Task.Run(static () => Console.WriteLine("hello"));
                         }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TaskRun_WithNonCapturingLambda_NoDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             Task.Run(() => Console.WriteLine("hello"));
                         }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TaskRun_WithMethodGroup_NoDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             Task.Run(DoWork);
                         }

                         static void DoWork() { }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TaskFactoryStartNew_WithStaticLambdaAndState_NoDiagnostic()
    {
        var source = """
                     using System;
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             var obj = new object();
                             Task.Factory.StartNew(static state => Console.WriteLine(state), obj);
                         }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnrelatedMethod_WithCapturingLambda_NoDiagnostic()
    {
        var source = """
                     using System;

                     class C
                     {
                         static void Run(Action a) { }

                         void M()
                         {
                             int x = 1;
                             Run(() => Console.WriteLine(x));
                         }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TaskRun_WithAsyncNonCapturingLambda_NoDiagnostic()
    {
        var source = """
                     using System.Threading.Tasks;

                     class C
                     {
                         void M()
                         {
                             Task.Run(async () => await Task.Delay(1));
                         }
                     }
                     """;

        await Verifier.VerifyAnalyzerAsync(source);
    }
}

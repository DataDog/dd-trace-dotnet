// <copyright file="BeforeAfterAspectAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AspectAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.AspectAnalyzers.BeforeAfterAspectAnalyzer,
    Datadog.Trace.Tools.Analyzers.AspectAnalyzers.BeforeAfterAspectCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.AspectAnalyzers;

public class BeforeAfterAspectAnalyzerTests
{
    private const string DiagnosticId = BeforeAfterAspectAnalyzer.DiagnosticId;
    private const DiagnosticSeverity Severity = BeforeAfterAspectAnalyzer.Severity;

    // No diagnostics expected to show up
    [Fact]
    public async Task EmptySourceShouldNotHaveDiagnostics()
    {
        var test = string.Empty;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ShouldNotFlagMethodWithoutAttributes()
    {
        var method =
            """
            string TestMethod(string myParam)
            {
                // does something
                return myParam;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagMethodWithTryCatch()
    {
        var method =
            """
            [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            string TestMethod(string myParam)
            {
                try
                {
                    // does something
                    return myParam;
                }
                catch (Exception ex)
                {
                    // the contents don't actually matter here
                    return myParam;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagMethodWithTryCatchAndBlockExceptionFilter()
    {
        var method =
            """
            [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            string TestMethod(string myParam)
            {
                try
                {
                    // does something
                    return myParam;
                }
                catch (Exception ex) when (ex is not BlockException)
                {
                    // the contents don't actually matter here
                    return myParam;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagEmptyMethod()
    {
        var method =
            """
            [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            void TestMethod(string myParam)
            {
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldFlagExpressionBodiedMember()
    {
        var method =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:=> myParam|};
            """;

        var fixedMethod =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            return myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                            return myParam;
                        }
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodWithoutTryCatch()
    {
        var method =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        // does something
                        return myParam;
                    }|}
            """;

        var fixedMethod =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            // does something
                            return myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                            return myParam;
                        }
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodWithDerivedException()
    {
        var method =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            // does something
                            return myParam;
                        }
                        {|#0:catch (SystemException ex)
                        {
                            return myParam;
                        }|}
                    }
            """;

        var fixedMethod =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            // does something
                            return myParam;
                        }
                        catch (SystemException ex)
                        {
                            return myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                            return myParam;
                        }
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodWithFilter()
    {
        var method =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            // does something
                            return myParam;
                        }
                        {|#0:catch (Exception ex) when (ex.Message == "test")
                        {
                            return myParam;
                        }|}
                    }
            """;

        var fixedMethod =
            """
                    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        try
                        {
                            // does something
                            return myParam;
                        }
                        catch (Exception ex) when (ex.Message == "test")
                        {
                            return myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                            return myParam;
                        }
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    private static string GetTestCode(string testFragment)
        =>
            $$"""
              using System;
              using System.Collections.Generic;
              using System.Linq;
              using System.Text;
              using System.Threading;
              using System.Threading.Tasks;
              using System.Diagnostics;

              namespace ConsoleApplication1
              {
                  class TestClass
                  {
                      private static readonly IDatadogLogger Log = new DatadogLogging();

              {{testFragment}}
                  }
              
                  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
                  internal abstract class AspectAttribute : Attribute { } 
              
                  internal sealed class AspectMethodInsertBeforeAttribute : AspectAttribute
                  {
                      public AspectMethodInsertBeforeAttribute(string targetMethod) { }
                  }

                  internal sealed class AspectMethodInsertAfterAttribute : AspectAttribute { }
                  
                  interface IDatadogLogger
                  {
                      void Error(Exception? exception, string messageTemplate);
                  }
                  
                  class DatadogLogging : IDatadogLogger
                  {
                      public void Error(Exception? exception, string messageTemplate) { }
                  }

                  static class IastModule
                  {
                      public static readonly IDatadogLogger Log = new DatadogLogging();
                  }

                  class BlockException : Exception
                  {
                  }
              }
              """;
}

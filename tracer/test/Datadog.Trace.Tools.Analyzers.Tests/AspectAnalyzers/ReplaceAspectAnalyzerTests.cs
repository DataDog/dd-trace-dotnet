// <copyright file="ReplaceAspectAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AspectAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.AspectAnalyzers.ReplaceAspectAnalyzer,
    Datadog.Trace.Tools.Analyzers.AspectAnalyzers.ReplaceAspectCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.AspectAnalyzers;

public class ReplaceAspectAnalyzerTests
{
    private const string DiagnosticId = ReplaceAspectAnalyzer.DiagnosticId;
    private const DiagnosticSeverity Severity = ReplaceAspectAnalyzer.Severity;

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
    public async Task ShouldNotFlagCtorMethodWithoutAttributes()
    {
        var method =
            """
            TestClass TestMethod(string myParam)
            {
                // does something
                return new TestClass();
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagMethodWithCorrectFormat()
    {
        var method =
            """
            [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            string TestMethod(string myParam)
            {
                var result = myParam + "/";
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
                return result;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagVoidMethodWithCorrectFormat()
    {
        var method =
            """
            [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            void TestMethod(string myParam)
            {
                string.Concat(myParam, "/");
                try
                {
                    // does something
                }
                catch (Exception ex)
                {
                    // the contents don't actually matter here
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldNotFlagCtorWithCorrectFormat()
    {
        var method =
            """
            [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
            TestClass TestMethod(string myParam)
            {
                var result = new TestClass();

                try
                {
                    // does something
                }
                catch (Exception ex)
                {
                    // the contents don't actually matter here
                }

                return result;
            }
            """;

        await Verifier.VerifyAnalyzerAsync(GetTestCode(method));
    }

    [Fact]
    public async Task ShouldFlagExpressionBodiedMember()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:=> myParam|};
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_Nothing()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        // does something
                        return myParam;
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_NoTryCatch()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        var result = myParam + "/";

                        _ = myParam;

                        return result;
                    }|}
            """;

        var fixedMethod =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        var result = myParam + "/";
                        try
                        {
                            _ = myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_NoTryCatchMultiBlock()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        var result = myParam + "/";

                        _ = myParam;
                        var x = myParam + myParam;

                        return result;
                    }|}
            """;

        var fixedMethod =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        var result = myParam + "/";
                        try
                        {
                            _ = myParam;
                            var x = myParam + myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_MultiBlockTryCatch()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        var result = myParam + "/";

                        _ = myParam;
                        try
                        {
                            var x = myParam + myParam;
                        }
                        catch (Exception e)
                        {
                            // the contents don't actually matter here
                        }
            
                        return result;
                    }|}
            """;

        var fixedMethod =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        var result = myParam + "/";
                        try
                        {
                            _ = myParam;
                            try
                            {
                                var x = myParam + myParam;
                            }
                            catch (Exception e)
                            {
                                // the contents don't actually matter here
                            }
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_ReturnsWrongValue()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        var result = myParam + "/";
                    
                        try
                        {
                            // does something
                        }
                        catch (Exception ex)
                        {
                            // the contents don't actually matter here
                        }
                    
                        return myParam;
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_NotALocalDeclaration()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {|#0:{
                        string.Concat(myParam, "/");
                    
                        try
                        {
                            // does something
                        }
                        catch (Exception ex)
                        {
                            // the contents don't actually matter here
                        }
                    
                        return myParam;
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagMethodThatDoesntFitSpec_InsufficientCatch()
    {
        var method =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        var result = myParam + "/";

                        try
                        {
                            // does something
                        }
                        {|#0:catch (SystemException ex)
                        {
                            // using too narrow an exception here
                        }|}

                        return result;
                    }
            """;

        var fixedMethod =
            """
                    [AspectMethodReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    string TestMethod(string myParam)
                    {
                        var result = myParam + "/";

                        try
                        {
                            // does something
                        }
                        catch (SystemException ex)
                        {
                            // using too narrow an exception here
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
                    }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagCtorThatDoesntFitSpec_Nothing()
    {
        var method =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {|#0:{
                        // does something
                        return new();
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagCtorThatDoesntFitSpec_NoTryCatch()
    {
        var method =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {|#0:{
                        var result = new TestClass();

                        _ = myParam;

                        return result;
                    }|}
            """;

        var fixedMethod =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {|#0:{
                        var result = new TestClass();
                        try
                        {
                            _ = myParam;
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        var fix = GetTestCode(fixedMethod);
        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    [Fact]
    public async Task ShouldFlagCtorThatDoesntFitSpec_ReturnsWrongValue()
    {
        var method =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {|#0:{
                        var result = new TestClass();
                    
                        try
                        {
                            // does something
                        }
                        catch (Exception ex)
                        {
                            // the contents don't actually matter here
                        }
                    
                        return new TestClass();
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagCtorThatDoesntFitSpec_NotALocalDeclaration()
    {
        var method =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {|#0:{
                        new TestClass();

                        try
                        {
                            // does something
                        }
                        catch (Exception ex)
                        {
                            // the contents don't actually matter here
                        }
                    
                        return new TestClass();
                    }|}
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        var code = GetTestCode(method);
        // can't fix as no good options
        await Verifier.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task ShouldFlagCtorThatDoesntFitSpec_InsufficientCatch()
    {
        var method =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {
                        var result = new TestClass();

                        try
                        {
                            // does something
                        }
                        {|#0:catch (SystemException ex)
                        {
                            // using too narrow an exception here
                        }|}

                        return result;
                    }
            """;

        var fixedMethod =
            """
                    [AspectCtorReplace("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
                    TestClass TestMethod(string myParam)
                    {
                        var result = new TestClass();

                        try
                        {
                            // does something
                        }
                        catch (SystemException ex)
                        {
                            // using too narrow an exception here
                        }
                        catch (Exception ex)
                        {
                            IastModule.Log.Error(ex, $"Error invoking {nameof(TestClass)}.{nameof(TestMethod)}");
                        }

                        return result;
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
              
                  internal sealed class AspectCtorReplaceAttribute : AspectAttribute
                  {
                      public AspectCtorReplaceAttribute(string targetMethod) { }
                  }
              
                  internal sealed class AspectMethodReplaceAttribute : AspectAttribute
                  {
                      public AspectMethodReplaceAttribute(string targetMethod) { }
                  }
                  
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
              }
              """;
}

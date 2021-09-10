// <copyright file="ThreadAbortAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer.ThreadAbortAnalyzer,
    Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer.ThreadAbortCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests
{
    public class ThreadAbortAnalyzerTests
    {
        private const string DiagnosticId = ThreadAbortAnalyzer.ThreadAbortAnalyzer.DiagnosticId;

        public static TheoryData<string> GetExceptions() => new() { "Exception", "SystemException", "ThreadAbortException" };

        // No diagnostics expected to show up
        [Fact]
        public async Task EmptySourceShouldNotHaveDiagnostics()
        {
            var test = string.Empty;

            await Verifier.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [MemberData(nameof(GetExceptions))]
        public async Task ShouldNotFlagWhileLoopThatRethrows(string exceptionType)
        {
            var code = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    catch (" + exceptionType + @")
                    {
                        Console.WriteLine(""ThreadAbortException"");
                        throw;
                    }
                    finally
                    {
                        Console.WriteLine(""Finally"");
                    }
                }");
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(GetExceptions))]
        public async Task ShouldNotFlagWhileLoopThatRethrowsInSecondCatchBlock(string exceptionType)
        {
            var code = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine(""ArgumentException"");
                    }
                    catch (" + exceptionType + @")
                    {
                        Console.WriteLine(""ThreadAbortException"");
                        throw;
                    }
                    finally
                    {
                        Console.WriteLine(""Finally"");
                    }
                }");
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(GetExceptions))]
        public async Task ShouldNotFlagWhileLoopThatRethrowsNewException(string exceptionType)
        {
            var code = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    catch (" + exceptionType + @" e)
                    {
                        Console.WriteLine(""ThreadAbortException"");
                        throw new ArgumentException();
                    }
                }");

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(GetExceptions))]
        public async Task ShouldFlagWhileLoopThatCatchesException(string exceptionType)
        {
            var code = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    {|#0:catch (" + exceptionType + @")
                    {
                        Console.WriteLine(""ThreadAbortException"");
                    }|}
                }");

            var fix = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    catch (" + exceptionType + @")
                    {
                        Console.WriteLine(""ThreadAbortException"");
                    throw;
                }
                }");

            var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
               .WithLocation(0);
            await Verifier.VerifyCodeFixAsync(code, expected, fix);
        }

        [Theory]
        [MemberData(nameof(GetExceptions))]
        public async Task ShouldFlagWhileLoopWithFinally(string exceptionType)
        {
            var code = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    {|#0:catch (" + exceptionType + @" e)
                    {
                        Console.WriteLine(""ThreadAbortException"");
                    }|}
                    finally
                    {
                        Console.WriteLine(""Finally"");
                    }
                }");
            var fix = GetTestCode(@"
                while (true)
                {
                    try
                    {
                        Console.WriteLine(""Thread loop"");
                    }
                    catch (" + exceptionType + @" e)
                    {
                        Console.WriteLine(""ThreadAbortException"");
                    throw;
                }
                    finally
                    {
                        Console.WriteLine(""Finally"");
                    }
                }");
            var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
               .WithLocation(0);
            await Verifier.VerifyCodeFixAsync(code, expected, fix);
        }

        private static string GetTestCode(string testFragment)
        {
            return @"
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
            public void TestMethod()
            {" + testFragment  + @"
            }
        }
    }";
        }
    }
}

// <copyright file="ThreadAbortAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Datadog.Trace.Tools.Analyzers.Tests.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer.ThreadAbortAnalyzer,
    Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer.ThreadAbortCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests
{
    [TestClass]
    public class ThreadAbortAnalyzerTests
    {
        private const string DiagnosticId = ThreadAbortAnalyzer.ThreadAbortAnalyzer.DiagnosticId;

        // No diagnostics expected to show up
        [TestMethod]
        public async Task EmptySourceShouldNotHaveDiagnostics()
        {
            var test = string.Empty;

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [DataTestMethod]
        [DataRow("Exception")]
        [DataRow("SystemException")]
        [DataRow("ThreadAbortException")]
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [DataTestMethod]
        [DataRow("Exception")]
        [DataRow("SystemException")]
        [DataRow("ThreadAbortException")]
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [DataTestMethod]
        [DataRow("Exception")]
        [DataRow("SystemException")]
        [DataRow("ThreadAbortException")]
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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [DataTestMethod]
        [DataRow("Exception")]
        [DataRow("SystemException")]
        [DataRow("ThreadAbortException")]
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
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(code, expected, fix);
        }

        [DataTestMethod]
        [DataRow("Exception")]
        [DataRow("SystemException")]
        [DataRow("ThreadAbortException")]
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
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(code, expected, fix);
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

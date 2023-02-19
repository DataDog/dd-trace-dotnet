// <copyright file="PascalPropertyNameDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.PascalCaseCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class PascalPropertyNameDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.LogAnalyzer
                                               .Diagnostics.PascalPropertyNameDiagnosticId;

    [Fact]
    public async Task UnrelatedSourceShouldNotHaveDiagnostics()
    {
        var test = $$"""
        {{Helpers.LoggerDefinitions}}

        public class MyTest {}
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Theory]
    [InlineData("Hello {Tester}", "foo")]
    [InlineData("Hello {1hello}", "foo")]
    [InlineData("Hello {1hello} {Tester}", "foo", "foo")]
    public async Task ShouldNotFlag_PascalProperties(string messageTemplate, params string[] logArgs)
    {
        var args = string.Join(", ", logArgs);
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.Debug("{{messageTemplate}}", {{args}});
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_WhenUsingAllLogMethods_PascalProperties(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {Tester}", foo);
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_EscapedVariables(string logMethod)
    {
        var src = $$$"""
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{{logMethod}}}("Hello {{im_escaped}} {ImNotButImPascal} ", foo);
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_NotPascalProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {|#0:{tester}|}", foo);
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {Tester}", foo);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
           .WithLocation(0)
                      .WithMessage("Property name 'tester' should be pascal case");
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_SnakeCase(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {|#0:{tester_name}|}", foo);
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {TesterName}", foo);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WhenTemplateHasEscapes_NotPascalProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello \"{|#0:{tester}|}\"", foo);
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello \"{Tester}\"", foo);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WhenTemplateHasVerbatimEscapes_NotPascalProperties(string logMethod)
    {
        var src = $$""""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}(@"Hello ""{|#0:{tester}|}""", foo);
            }
        }
        """";

        var fix = $$""""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}(@"Hello ""{Tester}""", foo);
            }
        }
        """";

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WhenHaveException_NotPascalProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Exception crashAndBurn = null;
                Log.{{logMethod}}(crashAndBurn, "{DwgFileName} Crashed and burned. {|#0:{stackTrace}|}", foo, crashAndBurn.StackTrace);
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Exception crashAndBurn = null;
                Log.{{logMethod}}(crashAndBurn, "{DwgFileName} Crashed and burned. {StackTrace}", foo, crashAndBurn.StackTrace);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WhenDestructuringObject_NotPascalProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {|#0:{@tester}|}", foo);
            }
        }
        """;

        var fix = $$""""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {@Tester}", foo);
            }
        }
        """";

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WhenStringifying_NotPascalProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {|#0:{$tester}|}", foo);
            }
        }
        """;

        var fix = $$""""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var foo = "tester";
                Log.{{logMethod}}("Hello {$Tester}", foo);
            }
        }
        """";

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }
}

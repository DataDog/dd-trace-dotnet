// <copyright file="ConstantMessageTemplateDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.ConstantMessageTemplateCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.Verifiers.XUnitVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.ConstantMessageTemplateCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class ConstantMessageTemplateDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Error;

    private const string DiagnosticId = Datadog.Trace.Tools.Analyzers.LogAnalyzer
                                               .Diagnostics.ConstantMessageTemplateDiagnosticId;

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_ConstantString(string logMethod)
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
                Log.{{logMethod}}("Some Value");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_EmptyString(string logMethod)
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
                Log.{{logMethod}}("");
                Log.{{logMethod}}(string.Empty);
                Log.{{logMethod}}(String.Empty);
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_NonConstantString(string logMethod)
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
                var message = GetString();
                Log.{{logMethod}}({|#0:message|});
            }

            public static string GetString() => "Some Value";
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("MessageTemplate argument message is not constant");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0}", "World")|});
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
                Log.{{logMethod}}("Hello {V}", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("MessageTemplate argument System.String.Format(\"Hello {0}\", \"World\") is not constant");
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithException(string logMethod)
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
                var ex = new System.Exception();
                Log.{{logMethod}}(ex, {|#0:System.String.Format("Hello {0}", "World")|});
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
                var ex = new System.Exception();
                Log.{{logMethod}}(ex, "Hello {V}", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithMultipleArgs(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0} to {1}", "Name", "World")|});
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
                Log.{{logMethod}}("Hello {V} to {V1}", "Name", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithStaticUsing(string logMethod)
    {
        var src = $$"""
        using static System.String;
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{logMethod}}({|#0:Format("Hello {0}", "World")|});
            }
        }
        """;

        var fix = $$"""
        using static System.String;
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{logMethod}}("Hello {V}", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithBadFormatString(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0} to {1}", "World")|});
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
                Log.{{logMethod}}("Hello {V} to {Error}", {|#0:"World"|}, null);
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithAlignment(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0,-10}", "World")|});
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
                Log.{{logMethod}}("Hello {V,-10}", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithFormat(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0:C}", 10)|});
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
                Log.{|#0:{{logMethod}}|}("Hello {V:C}", 10);
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix, expectedFixDiagnosticError: "CS0121");
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithFormatAndAlignment(string logMethod)
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
                Log.{{logMethod}}({|#0:System.String.Format("Hello {0,-10:C}", 10)|});
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
                Log.{|#0:{{logMethod}}|}("Hello {V,-10:C}", 10);
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix, expectedFixDiagnosticError: "CS0121");
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithAdditionalArgs(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{{logMethod}}}({|#0:System.String.Format("Hello {{Name}} to {0}", "World")|}, "Name");
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
                Log.{{logMethod}}("Hello {Name} to {V}", "Name", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithAdditionalArgsAndException(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var ex = new Exception();
                Log.{{{logMethod}}}(ex, {|#0:System.String.Format("Hello {{Name}} to {0}", "World")|}, "Name");
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
                var ex = new Exception();
                Log.{{logMethod}}(ex, "Hello {Name} to {V}", "Name", "World");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithMissingAdditionalArgs(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{{logMethod}}}({|#0:System.String.Format("Hello {{Name}} to {0}", "World")|});
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
                Log.{{logMethod}}("Hello {Name} to {V}", {|#0:null|}, "World");
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringFormat_WithEverythingMissing(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                Log.{{{logMethod}}}({|#0:System.String.Format("Hello {{Name}} to {0}")|});
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
                Log.{{logMethod}}("Hello {Name} to {Error}", {|#0:null|}, null);
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InterpolatedString(string logMethod)
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
                string name = "World";
                Log.{{logMethod}}({|#0:$"Hello {name}"|});
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
                string name = "World";
                Log.{{logMethod}}("Hello {Name}", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("MessageTemplate argument $\"Hello {name}\" is not constant");

        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InterpolatedString_WithException(string logMethod)
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
                var ex = new Exception();
                string name = "World";
                Log.{{logMethod}}(ex, {|#0:$"Hello {name}"|});
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
                var ex = new Exception();
                string name = "World";
                Log.{{logMethod}}(ex, "Hello {Name}", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InterpolatedString_WithAdditionalArgs(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                string world = "World";
                Log.{{{logMethod}}}({|#0:$"Hello {{Name}} to {world}"|}, "Name");
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
                string world = "World";
                Log.{{logMethod}}("Hello {Name} to {World}", "Name", world);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InterpolatedString_WithAdditionalArgsAndException(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var ex = new Exception();
                var world = "World";
                Log.{{{logMethod}}}(ex, {|#0:$"Hello {{Name}} to {world}"|}, "Name");
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
                var ex = new Exception();
                var world = "World";
                Log.{{logMethod}}(ex, "Hello {Name} to {World}", "Name", world);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InterpolatedString_WithMissingAdditionalArgs(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                var world = "World";
                Log.{{{logMethod}}}({|#0:$"Hello {{Name}} to {world}"|});
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
                var world = "World";
                Log.{{logMethod}}("Hello {Name} to {World}", {|#0:null|}, world);
            }
        }
        """;

        await VerifyWithExpectedCompileError(src, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringConcat(string logMethod)
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
                string name = "Tester";
                Log.{{logMethod}}({|#0:"Hello " + name|});
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
                string name = "Tester";
                Log.{{logMethod}}("Hello {Name}", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("MessageTemplate argument \"Hello \" + name is not constant");

        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringConcat_WithLineBreak(string logMethod)
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
                string name = "Tester";
                Log.{{logMethod}}({|#0:"Hello World\nName: '" + name + "'\n"|});
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
                string name = "Tester";
                Log.{{logMethod}}("Hello World\nName: '{Name}'\n", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringConcat_WithVerbatim(string logMethod)
    {
        var escapedNewLine = Environment.NewLine.Length > 1 ? @"\r\n" : @"\n";
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = DatadogLogging.GetLoggerFor<TypeName>();
            public static void Test()
            {
                string name = "Tester";
                Log.{{logMethod}}({|#0:@"Hello World
        Name: '" + name + "'{{escapedNewLine}}"|});
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
                string name = "Tester";
                Log.{{logMethod}}(@"Hello World
        Name: '{Name}'
        ", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringConcat_WithVerbatimIgnored(string logMethod)
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
                string name = "Tester";
                Log.{{logMethod}}({|#0:@"Hello World\nName: '" + name + @"'"|});
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
                string name = "Tester";
                Log.{{logMethod}}("Hello World\\nName: '{Name}'", name);
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringConcat_Complex(string logMethod)
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
                bool test = true;
                string name = "Tester";
                Log.{{logMethod}}({|#0:"Hello " + name + " to the {Place} " + (test ? " yes" + " no" : " no" + " yes") + " text"|}, "party");
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
                bool test = true;
                string name = "Tester";
                Log.{{logMethod}}("Hello {Name} to the {Place} {V} text", name, "party", (test ? " yes" + " no" : " no" + " yes"));
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        await Verifier.VerifyCodeFixAsync(src, expected, fix);
    }

    private static async Task VerifyWithExpectedCompileError(string src, string fix, string expectedFixDiagnosticError = "CS1503")
    {
        var expected = new DiagnosticResult(DiagnosticId, Severity).WithLocation(0);
        // Note that we still have an error in the fixed code due to type args - it's just best-effort
        var expectedInFix = new DiagnosticResult(expectedFixDiagnosticError, DiagnosticSeverity.Error).WithLocation(0);

        await Helpers.VerifyWithExpectedCompileError<
            Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
            Datadog.Trace.Tools.Analyzers.LogAnalyzer.ConstantMessageTemplateCodeFixProvider>(
            src, expected, fix, expectedInFix);
    }
}

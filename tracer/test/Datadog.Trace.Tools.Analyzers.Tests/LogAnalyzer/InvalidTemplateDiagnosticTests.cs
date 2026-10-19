// <copyright file="InvalidTemplateDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.LogAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class InvalidTemplateDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Error;
    private const string DiagnosticId = Diagnostics.TemplateDiagnosticId;

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InvalidFormat(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name:{|#0:$|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character '$' in property format");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InvalidAlignment(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:b|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character 'b' in property alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithAlignmentAndInvalidFormat(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,1:{|#0:$|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character '$' in property format");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithMissingAlignment(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name{|#0:,|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found alignment specifier without alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithZeroAlignment(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:0|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found zero size alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithOverflowingAlignment(string logMethod)
    {
        // An alignment of valid characters but too large for an Int32 must be a diagnostic, not a crash.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:99999999999|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid width in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithOverflowingNegativeAlignment(string logMethod)
    {
        // A negative alignment whose magnitude overflows Int32 must be a diagnostic, not a crash.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:-99999999999|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid width in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithLoneDashAlignment(string logMethod)
    {
        // A bare '-' alignment (no digits) must be a diagnostic, not a crash.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:-|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid width in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithNonAsciiDigitAlignment(string logMethod)
    {
        // char.IsDigit accepts non-ASCII decimal digits, but the integer parser does not; must be a diagnostic, not a crash.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:\u0669\u0669|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid width in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_WithMaxInt32Alignment(string logMethod)
    {
        // int.MaxValue is a representable width and must produce no diagnostic.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,2147483647} to the World", "test");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_JustOverMaxInt32Alignment(string logMethod)
    {
        // int.MaxValue + 1 is not representable and must be a diagnostic, not a crash.
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,{|#0:2147483648|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid width in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InvalidNegativeAlignment(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {Name,1{|#0:-|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("'-' character must be the first in alignment");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_UnclosedBrace(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {|#0:{Name to the World|}", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Encountered end of messageTemplate while parsing property");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_UnclosedBraceWithEscapes(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}(@"Hello {|#0:{Name to ""the"" World|}", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Encountered end of messageTemplate while parsing property");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_PropertyWithoutName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {|#0:{}|} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found property without name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_InvalidName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {{|#0:*|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character '*' in property name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_DestructuringWithoutName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {|#0:{@}|} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found property with destructuring hint but without name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringifyingWithoutName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {|#0:{$}|} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found property with destructuring hint but without name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_DestructuringAndInvalidName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {@{|#0: |}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character ' ' in property name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_StringifyingAndInvalidName(string logMethod)
    {
        var src = $$$"""
        using System;
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{{logMethod}}}("Hello {${|#0:*|}} to the World", "test");
            }
        }
        """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Found invalid character '*' in property name");

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }
}

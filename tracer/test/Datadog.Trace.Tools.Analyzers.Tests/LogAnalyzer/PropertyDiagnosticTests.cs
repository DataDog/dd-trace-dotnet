// <copyright file="PropertyDiagnosticTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.LogAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.LogAnalyzer,
    Datadog.Trace.Tools.Analyzers.LogAnalyzer.ExceptionPositionCodeFixProvider>;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class PropertyDiagnosticTests
{
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Error;
    private const string PropertyBindingDiagnosticId = Diagnostics.PropertyBindingDiagnosticId;
    private const string ExceptionDiagnosticId = Diagnostics.ExceptionDiagnosticId;
    private const string UniqueDiagnosticId = Diagnostics.UniquePropertyNameDiagnosticId;

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_MatchingPropertiesAndArgs(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {Tester}", "tester");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_MatchingPropertiesAndArgsWhenUsingArray(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {Tester1} {Tester2}", new object[] {"tester1", "tester2"});
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_MatchingPropertiesAndArgsWhenUsingNullableArray(string logMethod)
    {
        var src = $$"""
        #nullable enable
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {Tester1} {Tester2}", new object?[] {"tester1", "tester2"});
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_MatchingPropertiesAndArgsWhenUsingCollectionInitializer(string logMethod)
    {
        var src = $$"""
        #nullable enable
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {Tester1} {Tester2}", ["tester1", "tester2"]);
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_MatchingPositionalPropertiesAndArgs(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {0}", "tester");
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_ExceptionAsArgument(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                try
                {
                }
                catch (ArgumentException ex)
                {
                    log.{{logMethod}}("Hello World", {|#0:ex|});
                }
            }
        }
        """;

        var expected = new[]
        {
            new DiagnosticResult(ExceptionDiagnosticId, Severity)
               .WithLocation(0)
               .WithMessage("The exception 'ex' should be passed as first argument"),
            new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
               .WithLocation(0)
               .WithMessage("There is no property that corresponds to this argument"),
        };

        // Can't test a code fix for this because the verifier infrastructure doesn't like the
        // fact that we "accidentally" fix the property binding as far as I can tell.
        // Not worth the hassle to pursue.
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_ExceptionAsSecondArgument(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                try
                {
                }
                catch (ArgumentException ex)
                {
                    log.{{logMethod}}("Hello World {Ex}", {|#0:ex|});
                }
            }
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                try
                {
                }
                catch (ArgumentException ex)
                {
                    log.{{logMethod}}(ex, "Hello World {|#0:{Ex}|}");
                }
            }
        }
        """;

        var expected = new DiagnosticResult(ExceptionDiagnosticId, Severity).WithLocation(0);
        var fixExpected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity).WithLocation(0);

        await Helpers.VerifyWithExpectedCompileError<Analyzers.LogAnalyzer.LogAnalyzer, ExceptionPositionCodeFixProvider>(
            src, expected, fix, fixExpected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_ExceptionFromMethodAsArgument(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                try
                {
                }
                catch (ArgumentException ex)
                {
                    log.{{logMethod}}("Hello World {Ex}", {|#0:TestMethod(ex)|});
                }
            }

            public static Exception TestMethod(Exception ex) => ex;
        }
        """;

        var fix = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                try
                {
                }
                catch (ArgumentException ex)
                {
                    log.{{logMethod}}(TestMethod(ex), "Hello World {|#0:{Ex}|}");
                }
            }

            public static Exception TestMethod(Exception ex) => ex;
        }
        """;

        var expected = new DiagnosticResult(ExceptionDiagnosticId, Severity).WithLocation(0);
        var fixExpected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity).WithLocation(0);

        await Helpers.VerifyWithExpectedCompileError<Analyzers.LogAnalyzer.LogAnalyzer, ExceptionPositionCodeFixProvider>(
            src, expected, fix, fixExpected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MoreArgsThanProperties(string logMethod)
    {
        var src = $$"""
        using System;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                Datadog.Trace.Logging.IDatadogLogger log = null;
                log.{{logMethod}}("{User} did {Action}", "tester", "knock over", {|#0:"a sack of rice"|});
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no named property that corresponds to this argument");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MoreArgsThanPropertiesArray(string logMethod)
    {
        var src = $$$"""
        using Datadog.Trace.Logging;

        {{{Helpers.LoggerDefinitions}}}

        class TypeName
        {
            public static void Test()
            {
                Datadog.Trace.Logging.IDatadogLogger log = null;
                log.{{{logMethod}}}("{User} did {Action}", new object[] {"tester", "knock over", {|#0:"a sack of rice"|}});
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no named property that corresponds to this argument");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MorePropertiesThanArgs(string logMethod)
    {
        var src = $$"""
        using System;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                Datadog.Trace.Logging.IDatadogLogger log = null;
                log.{{logMethod}}("{User} did {Action} {|#0:{Subject}|}", "tester", "knock over");
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no argument that corresponds to the named property 'Subject'");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MorePropertiesThanArgsArray(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                Log.{{logMethod}}("Hello {Tester1} {|#0:{Tester2}|}", new object[] {"tester1"});
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no argument that corresponds to the named property 'Tester2'");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory(Skip = "We'd like to support this one, but analysis is a pain, so skipping it as it's an edge case")]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MorePropertiesThanArgsArrayWithPassedInArray(string logMethod)
    {
        var src = $$"""
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            private static IDatadogLogger Log = null;
            public static void Test()
            {
                var args = new object[] {"tester1"}; 
                Log.{{logMethod}}("Hello {Tester1} {|#0:{Tester2}|}", args);
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no argument that corresponds to the named property 'Tester2'");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_LargerPositionalPropertyThanArgs(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("{|#0:{1}|}");
            }
        }
        """;

        var expected = new[]
        {
            new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
               .WithLocation(0)
               .WithMessage("There is no argument that corresponds to the positional property 1")
        };

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MoreArgsThanPositionalProperty(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("{0}", "Mr.", {|#0:"Tester"|});
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no positional property that corresponds to this argument");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MoreArgsThanPositionalPropertyWhenUnused(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("{1}", {|#0:"Mr."|}, "Tester");
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no positional property that corresponds to this argument");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_NoPropertiesWithArg(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("", {|#0:"Tester"|});
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("There is no property that corresponds to this argument");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MixedPositionalAndNamedProperties(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("{|#0:{0}|} mixed with {Kind} Property", "positional", "named");
            }
        }
        """;

        var expected = new DiagnosticResult(PropertyBindingDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Positional properties are not allowed, when named properties are being used");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_DuplicatePropertyName(string logMethod)
    {
        var src = $$"""
        using System;
        using Datadog.Trace.Logging;

        {{Helpers.LoggerDefinitions}}

        class TypeName
        {
            public static void Test()
            {
                IDatadogLogger log = null;
                log.{{logMethod}}("{Tester} chats with {|#0:{Tester}|}", "tester1", "tester2");
            }
        }
        """;

        var expected = new DiagnosticResult(UniqueDiagnosticId, Severity)
                      .WithLocation(0)
                      .WithMessage("Property name 'Tester' is not unique in this MessageTemplate");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }
}

// <copyright file="NumericToStringInLogAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AllocationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.AllocationAnalyzer.NumericToStringInLogAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.AllocationAnalyzer;

public class NumericToStringInLogAnalyzerTests
{
    private const string DiagnosticId = Diagnostics.NumericToStringInLogDiagnosticId;
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    [Fact]
    public async Task EmptySourceShouldNotHaveDiagnostics()
    {
        var test = string.Empty;
        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_IntToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}("Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'count.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_LongToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    long size = 123456L;
                    Log.{{logMethod}}("Size is {Size}", {|#0:size.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'size.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_DoubleToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    double value = 3.14;
                    Log.{{logMethod}}("Value is {Value}", {|#0:value.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'value.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_ExpressionToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int attempt = 0;
                    Log.{{logMethod}}("Attempt {Attempt}", {|#0:(attempt + 1).ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary '(attempt + 1).ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_PropertyAccessToString(string logMethod)
    {
        var src = $$"""
            using System.Collections.Generic;
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    var list = new List<int>();
                    Log.{{logMethod}}("Count is {Count}", {|#0:list.Count.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'list.Count.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_WithExceptionOverload(string logMethod)
    {
        var src = $$"""
            using System;
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Exception ex = null;
                    Log.{{logMethod}}(ex, "Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'count.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MultipleArgs_OneFlagged(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    string name = "test";
                    int count = 42;
                    Log.{{logMethod}}("{Name} has {Count}", name, {|#0:count.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'count.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_MultipleToStringCalls(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int a = 1;
                    int b = 2;
                    Log.{{logMethod}}("{A} and {B}", {|#0:a.ToString()|}, {|#1:b.ToString()|});
                }
            }
            """;

        var expected = new[]
        {
            new DiagnosticResult(DiagnosticId, Severity)
                .WithLocation(0)
                .WithMessage("Remove unnecessary 'a.ToString()' call — the generic log overload handles numeric formatting without allocating a string"),
            new DiagnosticResult(DiagnosticId, Severity)
                .WithLocation(1)
                .WithMessage("Remove unnecessary 'b.ToString()' call — the generic log overload handles numeric formatting without allocating a string"),
        };
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_ExplicitGenericTypeArgs(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}<string>("Count is {Count}", {|#0:count.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'count.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldFlag_DecimalToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    decimal value = 9.99m;
                    Log.{{logMethod}}("Price is {Price}", {|#0:value.ToString()|});
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Remove unnecessary 'value.ToString()' call — the generic log overload handles numeric formatting without allocating a string");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_ToStringWithFormatArg(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}("Count is {Count}", count.ToString("N0"));
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_StringToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    string name = "test";
                    Log.{{logMethod}}("Name is {Name}", name.ToString());
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_ObjectToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    object obj = new object();
                    Log.{{logMethod}}("Obj is {Obj}", obj.ToString());
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_EnumToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            enum MyEnum { A, B }

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    var value = MyEnum.A;
                    Log.{{logMethod}}("Value is {Value}", value.ToString());
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_NonLogMethod()
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                public static string Test()
                {
                    int count = 42;
                    return count.ToString();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_ObjectArrayOverload(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}("Count is {Count}", new object[] { count.ToString() });
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task ShouldNotFlag_DifferentLoggerType()
    {
        var src = """
            using Datadog.Trace.Vendors.Serilog;

            namespace Datadog.Trace.Vendors.Serilog
            {
                internal interface ILogger
                {
                    void Debug(string messageTemplate);
                    void Debug<T>(string messageTemplate, T property);
                }
            }

            class TypeName
            {
                private static ILogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.Debug("Count is {Count}", count.ToString());
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_NoToString(string logMethod)
    {
        var src = $$"""
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    double count = 42.0;
                    Log.{{logMethod}}("Count is {Count}", count);
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Theory]
    [MemberData(nameof(Helpers.LogMethods), MemberType = typeof(Helpers))]
    public async Task ShouldNotFlag_ToStringWithCultureInfo(string logMethod)
    {
        var src = $$"""
            using System.Globalization;
            using Datadog.Trace.Logging;

            {{Helpers.LoggerDefinitions}}

            class TypeName
            {
                private static IDatadogLogger Log = null;
                public static void Test()
                {
                    int count = 42;
                    Log.{{logMethod}}("Count is {Count}", count.ToString(CultureInfo.InvariantCulture));
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }
}

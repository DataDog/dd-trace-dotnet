// <copyright file="ConfigurationSourceAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers.ConfigurationSourceAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

public class ConfigurationSourceAnalyzerTests
{
    private const string DD0011 = "DD0011"; // Hardcoded string literal
    private const string DD0012 = "DD0012"; // Variable or expression

    // Common source code snippets
    private const string IConfigurationSourceInterface = """
        #nullable enable
        namespace Datadog.Trace.Configuration;

        public interface IConfigurationSource
        {
            string? GetString(string key);
            int? GetInt32(string key);
            double? GetDouble(string key);
            bool? GetBool(string key);
            bool IsPresent(string key);
        }

        public class MyConfigurationSource : IConfigurationSource
        {
            public string? GetString(string key) => null;
            public int? GetInt32(string key) => null;
            public double? GetDouble(string key) => null;
            public bool? GetBool(string key) => null;
            public bool IsPresent(string key) => false;
        }

        public interface IConfigurationTelemetry { }
        """;

    private const string ConfigurationKeysClass = """
        #nullable enable
        namespace Datadog.Trace.Configuration;

        public static class ConfigurationKeys
        {
            public const string TraceEnabled = "DD_TRACE_ENABLED";
            public const string ServiceName = "DD_SERVICE";
            public const string MaxTracesSubmittedPerSecond = "DD_MAX_TRACES_SUBMITTED_PER_SECOND";

            public static class OpenTelemetry
            {
                public const string ExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
            }
        }
        """;

    private const string PlatformKeysClass = """
        #nullable enable
        namespace Datadog.Trace.Configuration;

        public static class PlatformKeys
        {
            public const string ProcessId = "process_id";
            public const string RuntimeId = "runtime_id";
        }
        """;

    [Theory]
    [InlineData("ConfigurationKeys.TraceEnabled", "GetString")]
    [InlineData("PlatformKeys.ProcessId", "GetString")]
    [InlineData("ConfigurationKeys.MaxTracesSubmittedPerSecond", "GetInt32")]
    [InlineData("ConfigurationKeys.OpenTelemetry.ExporterOtlpEndpoint", "GetString")]
    public async Task ValidGetMethodsUsingConfigurationKeys_ShouldHaveNoDiagnostics(string keyExpression, string methodName)
    {
        var test = CreateTestWithKeys($$"""
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class TestClass
            {
                public void TestMethod()
                {
                    var source = new MyConfigurationSource();
                    var value = source.{{methodName}}({{keyExpression}});
                }
            }
            """);

        await test.RunAsync();
    }

    [Theory]
    [InlineData("GetString", "DD_TRACE_ENABLED", DD0011)]
    [InlineData("GetInt32", "DD_MAX_TRACES", DD0011)]
    [InlineData("GetBool", "DD_TRACE_DEBUG", DD0011)]
    [InlineData("GetDouble", "DD_TRACE_SAMPLE_RATE", DD0011)]
    public async Task HardcodedStringInGetMethods_ShouldReportDiagnostic(string methodName, string keyValue, string diagnosticId)
    {
        var test = CreateTest(
            $$"""
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class TestClass
            {
                public void TestMethod()
                {
                    var source = new MyConfigurationSource();
                    var value = source.{{methodName}}({|#0:"{{keyValue}}"|});
                }
            }
            """,
            new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(methodName, keyValue));

        await test.RunAsync();
    }

    [Theory]
    [InlineData("GetInt32", "myKey")]
    [InlineData("GetString", "keyVariable")]
    [InlineData("GetDouble", "GetKey()")]
    public async Task VariableOrExpressionInGetMethods_ShouldReportDiagnostic(string methodName, string argument)
    {
        var test = CreateTest(
            $$"""
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class TestClass
            {
                private string GetKey() => "DD_KEY";

                public void TestMethod()
                {
                    var source = new MyConfigurationSource();
                    var myKey = "DD_MAX_TRACES";
                    var keyVariable = "DD_SERVICE";
                    var value = source.{{methodName}}({|#0:{{argument}}|});
                }
            }
            """,
            new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(methodName, argument));

        await test.RunAsync();
    }

    [Fact]
    public async Task InvalidConstantFromDifferentClass_ShouldReportDiagnostic()
    {
        var additionalCode = """
            #nullable enable
            namespace MyNamespace;

            public static class MyKeys
            {
                public const string MyKey = "MY_KEY";
            }
            """;

        var testCode = """
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class TestClass
            {
                public void TestMethod()
                {
                    var source = new MyConfigurationSource();
                    var value = source.GetString({|#0:MyNamespace.MyKeys.MyKey|});
                }
            }
            """;

        var test = CreateTest(
            additionalCode,
            testCode,
            new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GetString", "MyNamespace.MyKeys.MyKey"));

        await test.RunAsync();
    }

    [Fact]
    public async Task SelectorsClassInConfigurationBuilder_ShouldBeExcluded()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    """
                    #nullable enable
                    using System;
                    namespace Datadog.Trace.Configuration;

                    public interface IConfigurationSource
                    {
                        string? GetString(string key);
                        int? GetInt32(string key);
                    }

                    public interface IConfigurationTelemetry { }
                    """,
                    """
                    #nullable enable
                    using System;
                    namespace Datadog.Trace.Configuration.Telemetry;

                    internal readonly struct ConfigurationBuilder
                    {
                        private static class Selectors
                        {
                            // These lambda expressions should NOT trigger the analyzer
                            // because they're just forwarding the key parameter
                            internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, string?> AsString
                                = (source, key, telemetry) => source.GetString(key);

                            internal static readonly Func<IConfigurationSource, string, IConfigurationTelemetry, int?> AsInt32
                                = (source, key, telemetry) => source.GetInt32(key);
                        }
                    }
                    """
                }
            }
        };

        await test.RunAsync();
    }

    [Fact]
    public async Task PrivateMethodsInConfigurationBuilder_ShouldBeExcluded()
    {
        var test = CreateTest("""
            #nullable enable
            namespace Datadog.Trace.Configuration.Telemetry;

            internal readonly struct ConfigurationBuilder
            {
                private IConfigurationSource Source { get; }

                // Private methods should NOT trigger the analyzer
                private string? GetStringInternal(string key) => Source.GetString(key);
                private int? GetInt32Internal(string key) => Source.GetInt32(key);
            }
            """);

        await test.RunAsync();
    }

    [Fact]
    public async Task PrivateMethodsInNestedStructWithinConfigurationBuilder_ShouldBeExcluded()
    {
        var test = CreateTest("""
            #nullable enable
            namespace Datadog.Trace.Configuration.Telemetry;

            internal readonly struct ConfigurationBuilder
            {
                internal readonly struct HasKeys
                {
                    private IConfigurationSource Source { get; }
                    private IConfigurationTelemetry Telemetry { get; }

                    // Private methods in nested struct should NOT trigger the analyzer
                    private string? GetStringResult(string key)
                    {
                        var source = Source;
                        var telemetry = Telemetry;
                        return source.GetString(key);
                    }

                    private int? GetInt32Result(string key)
                    {
                        return Source.GetInt32(key);
                    }
                }
            }
            """);

        await test.RunAsync();
    }

    [Fact]
    public async Task MultipleViolationsInSameMethod_ShouldReportAllDiagnostics()
    {
        var test = CreateTest(
            """
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class TestClass
            {
                public void TestMethod()
                {
                    var source = new MyConfigurationSource();
                    var value1 = source.GetString({|#0:"DD_SERVICE"|});
                    var myVar = "DD_ENV";
                    var value2 = source.GetInt32({|#1:myVar|});
                }
            }
            """,
            new DiagnosticResult(DD0011, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GetString", "DD_SERVICE"),
            new DiagnosticResult(DD0012, DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("GetInt32", "myVar"));

        await test.RunAsync();
    }

    [Fact]
    public async Task MethodsInsideConfigurationSourceImplementation_ShouldBeExcluded()
    {
        var test = CreateTest("""
            #nullable enable
            namespace Datadog.Trace.Configuration;

            public class CompositeConfigurationSource : IConfigurationSource
            {
                private readonly IConfigurationSource _first;
                private readonly IConfigurationSource _second;

                public string? GetString(string key)
                {
                    // IConfigurationSource implementations can use variables internally
                    var result = _first.GetString(key);
                    if (result != null)
                    {
                        return result;
                    }
                    return _second.GetString(key);
                }

                public int? GetInt32(string key)
                {
                    // Can also use hardcoded strings for internal logic
                    var fallbackKey = "FALLBACK_KEY";
                    return _first.GetInt32(key) ?? _second.GetInt32(fallbackKey);
                }

                public double? GetDouble(string key) => _first.GetDouble(key) ?? _second.GetDouble(key);
                public bool? GetBool(string key) => _first.GetBool(key) ?? _second.GetBool(key);
                public bool IsPresent(string key) => _first.IsPresent(key) || _second.IsPresent(key);
            }
            """);

        await test.RunAsync();
    }

    private static Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier> CreateTest(
        string testCode,
        params DiagnosticResult[] expectedDiagnostics)
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { IConfigurationSourceInterface, testCode }
            }
        };

        foreach (var diagnostic in expectedDiagnostics)
        {
            test.TestState.ExpectedDiagnostics.Add(diagnostic);
        }

        return test;
    }

    private static Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier> CreateTest(
        string additionalCode,
        string testCode,
        params DiagnosticResult[] expectedDiagnostics)
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { IConfigurationSourceInterface, additionalCode, testCode }
            }
        };

        foreach (var diagnostic in expectedDiagnostics)
        {
            test.TestState.ExpectedDiagnostics.Add(diagnostic);
        }

        return test;
    }

    private static Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier> CreateTestWithKeys(
        string testCode,
        params DiagnosticResult[] expectedDiagnostics)
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<ConfigurationSourceAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { IConfigurationSourceInterface, ConfigurationKeysClass, PlatformKeysClass, testCode }
            }
        };

        foreach (var diagnostic in expectedDiagnostics)
        {
            test.TestState.ExpectedDiagnostics.Add(diagnostic);
        }

        return test;
    }
}

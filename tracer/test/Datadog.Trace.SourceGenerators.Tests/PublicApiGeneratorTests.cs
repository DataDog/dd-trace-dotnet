// <copyright file="PublicApiGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.PublicApi;
using Datadog.Trace.SourceGenerators.PublicApi.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class PublicApiGeneratorTests
{
    [Theory]
    [InlineData("=> null;")]
    [InlineData("{ get; }")]
    [InlineData("{ get; } = null;")]
    public void CanGenerateReadOnlyProperty(string definition)
    {
        var input = $$"""
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get)]
                internal string? _Environment {{definition}}
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? Environment
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return _Environment;
                    }
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Theory]
    [InlineData("GeneratePublicApi")]
    [InlineData("GeneratePublicApiAttribute")]
    [InlineData("Datadog.Trace.SourceGenerators.GeneratePublicApiAttribute")]
    public void CanGenerateReadWriteProperty(string attributeName)
    {
        var input = $$"""
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [{{attributeName}}(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? _Environment { get; set; }
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? Environment
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return _Environment;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        _Environment = value;
                    }
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Fact]
    public void CanGenerateMultipleProperties()
    {
        const string input = """
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? _Environment { get; set; }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [GeneratePublicApi(PublicApiUsage.ServiceName_Get)]
                internal HashSet<string> ServiceNameInternal { get; }
            }
            """;

        const string expected = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? Environment
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return _Environment;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        _Environment = value;
                    }
                }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [Datadog.Trace.SourceGenerators.PublicApi]
                public HashSet<string> ServiceName
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)2);
                        return ServiceNameInternal;
                    }
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Theory]
    [InlineData("Obsolete")]
    [InlineData("ObsoleteAttribute")]
    [InlineData("System.Obsolete")]
    public void CanHandleObsoleteProperties(string obsoleteAttribute)
    {
        var input = $$"""
            using System;
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                [{{obsoleteAttribute}}]
                internal string? NoArgsInternal { get; set; }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                [{{obsoleteAttribute}}("some reason")]
                internal string? OneArgInternal { get; set; }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                [{{obsoleteAttribute}}(Reason)]
                internal string? OneArg2Internal { get; set; }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                [{{obsoleteAttribute}}("some reason", true)]
                internal string? TwoArgsInternal { get; set; }

                private const string Reason = "some reason";
            }
            """;

        var expected = Constants.FileHeader + """
            namespace MyTests.TestMetricNameSpace;
            partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [System.Obsolete]
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? NoArgs
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return NoArgsInternal;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        NoArgsInternal = value;
                    }
                }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [System.Obsolete("some reason")]
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? OneArg
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return OneArgInternal;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        OneArgInternal = value;
                    }
                }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [System.Obsolete("some reason")]
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? OneArg2
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return OneArg2Internal;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        OneArg2Internal = value;
                    }
                }

                /// <summary>
                /// Gets the default service name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Service"/>
                [System.Obsolete("some reason")]
                [Datadog.Trace.SourceGenerators.PublicApi]
                public string? TwoArgs
                {
                    get
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)0);
                        return TwoArgsInternal;
                    }
                    set
                    {
                        Datadog.Trace.Telemetry.TelemetryFactory.Metrics.Record(
                            (Datadog.Trace.Telemetry.Metrics.PublicApiUsage)1);
                        TwoArgsInternal = value;
                    }
                }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();
        output.Should().Be(expected);
    }

    [Theory]
    [InlineData("struct")]
    [InlineData("record struct")]
    public void CanNotGenerateForStruct(string type)
    {
        var input = $$"""
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial {{type}} TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? _Environment { get; set; }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        Assert.Contains(diagnostics, diag => diag.Id == OnlySupportsClassesDiagnostic.Id);
    }

    [Theory]
    [InlineData("=> null;")]
    [InlineData("{ get; }")]
    [InlineData("{ get; } = null;")]
    public void CanNotGenerateSetterForReadOnlyProperty(string definition)
    {
        var input = $$"""
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? _Environment {{definition}}
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        Assert.Contains(diagnostics, diag => diag.Id == SetterOnReadonlyFieldDiagnostic.Id);
    }

    [Fact]
    public void ErrorsWhenCantDetermineName()
    {
        var input = """
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public partial class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? Environment { get; set; }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        Assert.Contains(diagnostics, diag => diag.Id == NamingProblemDiagnostic.Id);
    }

    [Fact]
    public void AddsDiagnosticWhenPartialModifierMissing()
    {
        var input = """
            using Datadog.Trace.Configuration;
            using Datadog.Trace.SourceGenerators;
            using Datadog.Trace.Telemetry.Metrics;

            #nullable enable
            namespace MyTests.TestMetricNameSpace;
            public class TestSettings
            {
                /// <summary>
                /// Gets the default environment name applied to all spans.
                /// </summary>
                /// <seealso cref="ConfigurationKeys.Environment"/>
                [GeneratePublicApi(PublicApiUsage.Environment_Get, PublicApiUsage.Environment_Set)]
                internal string? Environment { get; set; }
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput<PublicApiGenerator>(input, GetPublicApiAttribute());
        Assert.Contains(diagnostics, diag => diag.Id == PartialModifierIsRequiredDiagnostic.Id);
    }

    private static string GetPublicApiAttribute()
        => """
            namespace Datadog.Trace.Telemetry.Metrics;
            public enum PublicApiUsage 
            {
                Environment_Get,
                Environment_Set,
                ServiceName_Get,
            }
            """;
}

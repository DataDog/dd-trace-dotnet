// <copyright file="AnalyzerTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Datadog.Trace.Tools.Analyzers.Tests.ConfigurationAnalyzers;

internal static class AnalyzerTestHelper
{
    /// <summary>
    /// Minimal required type definitions to prevent DD0009 errors in tests.
    /// Does not include ConfigurationBuilder/HasKeys for tests that define these themselves.
    /// </summary>
    public const string MinimalRequiredTypes = """
        namespace Datadog.Trace.Configuration
        {
            public static partial class ConfigurationKeys { }
            public static partial class PlatformKeys { }
        }
        namespace Datadog.Trace.Util
        {
            public static partial class EnvironmentHelpers { public static string GetEnvironmentVariable(string key) => null; }
            public static partial class EnvironmentHelpersNoLogging { }
        }
        namespace Datadog.Trace.Ci.CiEnvironment
        {
            public interface IValueProvider { }
        }
        """;

    /// <summary>
    /// Complete required type definitions including ConfigurationBuilder and HasKeys.
    /// Use this for tests that don't define these types themselves.
    /// </summary>
    public const string RequiredTypes = MinimalRequiredTypes + """
        namespace Datadog.Trace.Configuration.Telemetry
        {
            public struct ConfigurationBuilder { public HasKeys WithKeys(string key) => default; }
            public struct HasKeys { }
        }
        """;

    /// <summary>
    /// Verifies analyzer with assembly name set to Datadog.Trace
    /// </summary>
    public static async Task VerifyDatadogAnalyzerAsync<TAnalyzer>(string source, params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source }
            }
        };

        test.TestState.ExpectedDiagnostics.AddRange(expected);
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, "Datadog.Trace"));
        await test.RunAsync();
    }
}

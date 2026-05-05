// <copyright file="RedundantEnvironmentRestorerAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.TestingAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.TestingAnalyzer.EnvironmentRestorerAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.TestingAnalyzer;

public class RedundantEnvironmentRestorerAnalyzerTests
{
    private const string DiagnosticId = Diagnostics.RedundantEnvironmentRestorerDiagnosticId;
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Info;

    [Fact]
    public async Task DuplicateAcrossMethods_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [{|#0:EnvironmentRestorer("MY_VAR")|}]
                public void TestA()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "a");
                }

                [Fact]
                [{|#1:EnvironmentRestorer("MY_VAR")|}]
                public void TestB()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "b");
                }
            }
            """;

        var expected0 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("[EnvironmentRestorer(\"MY_VAR\")] appears on multiple methods — move it to the class level instead");
        var expected1 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(1)
            .WithMessage("[EnvironmentRestorer(\"MY_VAR\")] appears on multiple methods — move it to the class level instead");
        await Verifier.VerifyAnalyzerAsync(src, expected0, expected1);
    }

    [Fact]
    public async Task SingleMethodWithRestorer_ShouldNotFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [EnvironmentRestorer("MY_VAR")]
                public void TestA()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "a");
                }

                [Fact]
                public void TestB()
                {
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task MethodRestorerAlreadyCoveredByClass_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            [EnvironmentRestorer("MY_VAR")]
            class TestClass
            {
                [Fact]
                [{|#0:EnvironmentRestorer("MY_VAR")|}]
                public void TestA()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "a");
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("[EnvironmentRestorer(\"MY_VAR\")] on method 'TestA' is already covered by the class-level attribute — remove it");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task PartialOverlap_ShouldFlagOnlyDuplicated()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [{|#0:EnvironmentRestorer("VAR_X", "VAR_Y")|}]
                public void TestA()
                {
                    Environment.SetEnvironmentVariable("VAR_X", "a");
                    Environment.SetEnvironmentVariable("VAR_Y", "a");
                }

                [Fact]
                [{|#1:EnvironmentRestorer("VAR_X")|}]
                public void TestB()
                {
                    Environment.SetEnvironmentVariable("VAR_X", "b");
                }
            }
            """;

        var expected0 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("[EnvironmentRestorer(\"VAR_X\")] appears on multiple methods — move it to the class level instead");
        var expected1 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(1)
            .WithMessage("[EnvironmentRestorer(\"VAR_X\")] appears on multiple methods — move it to the class level instead");
        await Verifier.VerifyAnalyzerAsync(src, expected0, expected1);
    }
}

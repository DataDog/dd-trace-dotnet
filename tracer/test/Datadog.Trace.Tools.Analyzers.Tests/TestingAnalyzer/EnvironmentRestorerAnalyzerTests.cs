// <copyright file="EnvironmentRestorerAnalyzerTests.cs" company="Datadog">
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

public class EnvironmentRestorerAnalyzerTests
{
    private const string DiagnosticId = Diagnostics.MissingEnvironmentRestorerDiagnosticId;
    private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

    [Fact]
    public async Task EmptySource_NoDiagnostics()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_NoRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                public void MyTest()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task TheoryMethod_SetEnvVar_NoRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Theory]
                public void MyTest()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task SkippableFactMethod_SetEnvVar_NoRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [SkippableFact]
                public void MyTest()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_MethodLevelRestorer_ShouldNotFlag()
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
                public void MyTest()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_ClassLevelRestorer_ShouldNotFlag()
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
                public void MyTest()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_WrongVariableInRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [EnvironmentRestorer("OTHER_VAR")]
                public void MyTest()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_InsideTryBlock_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                public void MyTest()
                {
                    try
                    {
                        {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                    }
                    finally
                    {
                        {|#1:Environment.SetEnvironmentVariable("MY_VAR", null)|};
                    }
                }
            }
            """;

        var expected0 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        var expected1 = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(1)
            .WithMessage("Environment variable 'MY_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected0, expected1);
    }

    [Fact]
    public async Task NonTestMethod_SetEnvVar_ShouldNotFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                public void NotATest()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_NonConstantName_NoRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                public void MyTest()
                {
                    string varName = "MY_VAR";
                    {|#0:Environment.SetEnvironmentVariable(varName, "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable is set using a non-constant name — use a constant for the variable name and add [EnvironmentRestorer] at the method or class level, or suppress with #pragma");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_NonConstantName_WithRestorer_ShouldStillFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [EnvironmentRestorer("SOME_VAR")]
                public void MyTest()
                {
                    string varName = "MY_VAR";
                    {|#0:Environment.SetEnvironmentVariable(varName, "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable is set using a non-constant name — use a constant for the variable name and add [EnvironmentRestorer] at the method or class level, or suppress with #pragma");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_ConstField_NoRestorer_ShouldFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                private const string MyVar = "MY_CONST_VAR";

                [Fact]
                public void MyTest()
                {
                    {|#0:Environment.SetEnvironmentVariable(MyVar, "value")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'MY_CONST_VAR' is set without a corresponding [EnvironmentRestorer(\"MY_CONST_VAR\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_MultipleSetEnvVar_PartialCoverage_ShouldFlagUncovered()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [EnvironmentRestorer("VAR_A")]
                public void MyTest()
                {
                    Environment.SetEnvironmentVariable("VAR_A", "a");
                    {|#0:Environment.SetEnvironmentVariable("VAR_B", "b")|};
                }
            }
            """;

        var expected = new DiagnosticResult(DiagnosticId, Severity)
            .WithLocation(0)
            .WithMessage("Environment variable 'VAR_B' is set without a corresponding [EnvironmentRestorer(\"VAR_B\")] attribute — add it at the method or class level to ensure the variable is restored after the test");
        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task FactMethod_SetEnvVar_MultipleVarsInRestorer_ShouldNotFlag()
    {
        var src = $$"""
            using System;
            using Xunit;
            using Datadog.Trace.TestHelpers;

            {{Helpers.TypeDefinitions}}

            class TestClass
            {
                [Fact]
                [EnvironmentRestorer("VAR_A", "VAR_B")]
                public void MyTest()
                {
                    Environment.SetEnvironmentVariable("VAR_A", "a");
                    Environment.SetEnvironmentVariable("VAR_B", "b");
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }
}

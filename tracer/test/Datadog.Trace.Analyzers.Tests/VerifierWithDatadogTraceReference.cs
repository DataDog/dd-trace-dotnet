// <copyright file="VerifierWithDatadogTraceReference.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Datadog.Trace.Analyzers.Tests;

#pragma warning disable SA1402 // File may only contain one type
public class VerifierWithDatadogTraceReference<TAnalyzer> : VerifierWithDatadogTraceReference<TAnalyzer, CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>, XUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
}

public class VerifierWithDatadogTraceReference<TAnalyzer, TCodeFix> : VerifierWithDatadogTraceReference<TAnalyzer, TCodeFix, CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>, XUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
}

public class VerifierWithDatadogTraceReference<TAnalyzer, TTest, TVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TTest : AnalyzerTest<TVerifier>, new()
    where TVerifier : IVerifier, new()
{
    public static DiagnosticResult Diagnostic()
    {
        var analyzer = new TAnalyzer();
        try
        {
            return Diagnostic(analyzer.SupportedDiagnostics.Single());
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"'{nameof(Diagnostic)}()' can only be used when the analyzer has a single supported diagnostic. Use the '{nameof(Diagnostic)}(DiagnosticDescriptor)' overload to specify the descriptor from which to create the expected result.",
                ex);
        }
    }

    public static DiagnosticResult Diagnostic(string diagnosticId)
    {
        var analyzer = new TAnalyzer();
        try
        {
            return Diagnostic(analyzer.SupportedDiagnostics.Single(i => i.Id == diagnosticId));
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"'{nameof(Diagnostic)}(string)' can only be used when the analyzer has a single supported diagnostic with the specified ID. Use the '{nameof(Diagnostic)}(DiagnosticDescriptor)' overload to specify the descriptor from which to create the expected result.",
                ex);
        }
    }

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new DiagnosticResult(descriptor);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new TTest
        {
            TestCode = source,
        };
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net50;
        var metadataReferences = new[] { MetadataReference.CreateFromFile(typeof(Datadog.Trace.Tracer).GetTypeInfo().Assembly.Location) };

        test.TestState.AdditionalReferences.AddRange(metadataReferences);

        test.ExpectedDiagnostics.AddRange(expected);

        return test.RunAsync(CancellationToken.None);
    }
}

public class VerifierWithDatadogTraceReference<TAnalyzer, TCodeFix, TTest, TVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
    where TTest : CodeFixTest<TVerifier>, new()
    where TVerifier : IVerifier, new()
{
    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic()"/>
    public static DiagnosticResult Diagnostic()
        => VerifierWithDatadogTraceReference<TAnalyzer, TTest, TVerifier>.Diagnostic();

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(string)"/>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => VerifierWithDatadogTraceReference<TAnalyzer, TTest, TVerifier>.Diagnostic(diagnosticId);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => VerifierWithDatadogTraceReference<TAnalyzer, TTest, TVerifier>.Diagnostic(descriptor);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => VerifierWithDatadogTraceReference<TAnalyzer, TTest, TVerifier>.VerifyAnalyzerAsync(source, expected);

    /// <summary>
    /// Verifies the analyzer provides diagnostics which, in combination with the code fix, produce the expected
    /// fixed code.
    /// </summary>
    /// <param name="source">The source text to test. Any diagnostics are defined in markup.</param>
    /// <param name="fixedSource">The expected fixed source text. Any remaining diagnostics are defined in markup.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task VerifyCodeFixAsync(string source, string fixedSource)
        => VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

    /// <summary>
    /// Verifies the analyzer provides diagnostics which, in combination with the code fix, produce the expected
    /// fixed code.
    /// </summary>
    /// <param name="source">The source text to test, which may include markup syntax.</param>
    /// <param name="expected">The expected diagnostic. This diagnostic is in addition to any diagnostics defined in
    /// markup.</param>
    /// <param name="fixedSource">The expected fixed source text. Any remaining diagnostics are defined in markup.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        => VerifyCodeFixAsync(source, new[] { expected }, fixedSource);

    /// <summary>
    /// Verifies the analyzer provides diagnostics which, in combination with the code fix, produce the expected
    /// fixed code.
    /// </summary>
    /// <param name="source">The source text to test, which may include markup syntax.</param>
    /// <param name="expected">The expected diagnostics. These diagnostics are in addition to any diagnostics
    /// defined in markup.</param>
    /// <param name="fixedSource">The expected fixed source text. Any remaining diagnostics are defined in markup.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
    {
        var test = new TTest
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net50;
        var metadataReferences = new[] { MetadataReference.CreateFromFile(typeof(Datadog.Trace.Tracer).GetTypeInfo().Assembly.Location) };
        test.TestState.AdditionalReferences.AddRange(metadataReferences);

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(CancellationToken.None);
    }
}

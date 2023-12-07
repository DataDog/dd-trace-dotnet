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
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Datadog.Trace.Analyzers.Test;

#pragma warning disable SA1402 // File may only contain one type
public class VerifierWithDatadogTraceReference<TAnalyzer> : VerifierWithDatadogTraceReference<TAnalyzer, CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>, XUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
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

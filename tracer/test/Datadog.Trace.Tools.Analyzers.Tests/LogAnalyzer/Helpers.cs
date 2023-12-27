// <copyright file="Helpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public static class Helpers
{
    public static TheoryData<string> LogMethods { get; } = new() { "Debug", "Information", "Warning", "Error" };

    public static string LoggerDefinitions { get; } =
        """
        #nullable enable

        namespace Datadog.Trace.Logging
        {
            using System;
            using Datadog.Trace.Vendors.Serilog.Events;

            internal static class DatadogLogging
            {
                public static IDatadogLogger GetLoggerFor(Type classType) => null;
                public static IDatadogLogger GetLoggerFor<T>() => null;
            }

            internal interface IDatadogLogger
            {
                bool IsEnabled(LogEventLevel level);
                void Debug(string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Debug<T>(string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Debug<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3, int sourceLine = 0, string sourceFile = "");
                void Debug(string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Debug(Exception? exception, string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Debug<T>(Exception? exception, string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Debug<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Debug<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Debug(Exception? exception, string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Information(string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Information<T>(string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Information(string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Information(Exception? exception, string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Information<T>(Exception? exception, string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Information<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Information<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Information(Exception? exception, string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Warning(string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Warning<T>(string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Warning(string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Warning(Exception? exception, string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Warning<T>(Exception? exception, string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Warning<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Warning<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Warning(Exception? exception, string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Error(string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Error<T>(string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Error(string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void Error(Exception? exception, string messageTemplate, int sourceLine = 0, string sourceFile = "");
                void Error<T>(Exception? exception, string messageTemplate, T property, int sourceLine = 0, string sourceFile = "");
                void Error<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1, int sourceLine = 0, string sourceFile = "");
                void Error<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2, int sourceLine = 0, string sourceFile = "");
                void Error(Exception? exception, string messageTemplate, object?[] args, int sourceLine = 0, string sourceFile = "");
                void CloseAndFlush();
            }
        }

        namespace Datadog.Trace.Vendors.Serilog.Events
        {
            internal enum LogEventLevel
            {
                Verbose,
                Debug,
                Information,
                Warning,
                Error,
                Fatal
            }
        }

        namespace Datadog.Trace.Vendors.Serilog
        {
            using Datadog.Trace.Vendors.Serilog.Events;
            internal interface ILogger
            {
                bool IsEnabled(LogEventLevel level);
                void Debug(string messageTemplate);
            }
        }
        """;

    public static Task VerifyWithExpectedCompileError<TAnalyzer, TCodeFix>(
        string src, DiagnosticResult initialDiagnostic, string fix, DiagnosticResult diagnosticInFix)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        return VerifyWithExpectedCompileError<TAnalyzer, TCodeFix>(src, new[] { initialDiagnostic }, fix, new[] { diagnosticInFix });
    }

    public static async Task VerifyWithExpectedCompileError<TAnalyzer, TCodeFix>(
        string src, DiagnosticResult[] initialDiagnostics, string fix, DiagnosticResult[] diagnosticsInFix)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
        {
            TestCode = src,
            FixedCode = fix,
        };
        test.ExpectedDiagnostics.AddRange(initialDiagnostics);
        test.FixedState.ExpectedDiagnostics.AddRange(diagnosticsInFix);

        await test.RunAsync(CancellationToken.None);
    }
}

// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.Helpers
{
    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor MissingRequiredType = new(
            id: "DD0009",
            title: "Required type not found for analyzer",
            messageFormat: "Analyzer {0} could not find required type '{1}' in Datadog.Trace. The type may have been renamed or removed. Please make sure the types haven't been renamed, and if the renaming was justified, update the analyzer.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The analyzer requires certain types to be present in the compilation of Datadog.Trace. If they are missing, they may have been renamed or removed.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

#pragma warning disable RS1013 // The CompilationStartAction in callers also registers other actions (SyntaxNodeAction/SymbolAction), so this is not a start/end-only pair
        /// <summary>
        /// Checks if a type is null and reports a diagnostic if compiling the Datadog.Trace assembly.
        /// Returns true if the type is null (caller should bail out).
        /// </summary>
        internal static bool IsTypeNullAndReportForDatadogTrace(
            CompilationStartAnalysisContext context,
            [NotNullWhen(false)] INamedTypeSymbol? type,
            string analyzerName,
            string typeName)
        {
            if (type is not null)
            {
                return false;
            }

            if (context.Compilation.AssemblyName == "Datadog.Trace")
            {
                var diagnostic = Diagnostic.Create(
                    MissingRequiredType,
                    Location.None,
                    analyzerName,
                    typeName);
                context.RegisterCompilationEndAction(c => c.ReportDiagnostic(diagnostic));
            }

            return true;
        }
#pragma warning restore RS1013
    }
}

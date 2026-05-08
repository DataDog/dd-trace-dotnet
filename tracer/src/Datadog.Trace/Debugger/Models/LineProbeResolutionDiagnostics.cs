// <copyright file="LineProbeResolutionDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;

namespace Datadog.Trace.Debugger.Models
{
    internal sealed record LineProbeResolutionDiagnostics(
        string? ProbeFile = null,
        int? ProbeLine = null,
        string? RawLines = null,
        string? ResolvedSourceFile = null,
        LineProbePathMatchType? PathMatchType = null,
        int? MatchingTrailingSegments = null,
        LineProbeFallbackFailureReason? FallbackFailureReason = null,
        int? QualifiedFallbackMatchCount = null,
        string? AssemblyName = null,
        string? AssemblyLocation = null,
        Guid? ModuleVersionId = null,
        string? ProbeId = null,
        string? ExceptionType = null,
        int LoadedAssemblyCount = 0,
        int SymbolicatedAssemblyCount = 0,
        int SameFileNameMatchCount = 0,
        string[]? SameFileNameExamples = null)
    {
        public string ToSummary()
        {
            var sameFileNameExamples = SameFileNameExamples is { Length: > 0 } ? string.Join(" | ", SameFileNameExamples) : "<none>";
            string?[] values =
            {
                Format("probeFile", ProbeFile),
                Format("probeLine", ProbeLine),
                Format("rawLines", RawLines),
                Format("resolvedSourceFile", ResolvedSourceFile),
                Format("pathMatchType", PathMatchType),
                Format("matchingTrailingSegments", MatchingTrailingSegments),
                Format("fallbackFailureReason", FallbackFailureReason is not null and not LineProbeFallbackFailureReason.None ? FallbackFailureReason : null),
                Format("qualifiedFallbackMatches", QualifiedFallbackMatchCount),
                Format("assemblyName", AssemblyName),
                Format("assemblyLocation", AssemblyLocation),
                Format("moduleVersionId", ModuleVersionId),
                Format("probeId", ProbeId),
                Format("exceptionType", ExceptionType),
                Format("loadedAssemblies", LoadedAssemblyCount > 0 ? LoadedAssemblyCount : null),
                Format("symbolicatedAssemblies", SymbolicatedAssemblyCount > 0 ? SymbolicatedAssemblyCount : null),
                Format("sameFileNameMatches", SameFileNameMatchCount > 0 ? SameFileNameMatchCount : null),
                SameFileNameExamples is { Length: > 0 } ? $"sameFileNameExamples={sameFileNameExamples}" : null
            };

            return string.Join("; ", values.Where(v => !string.IsNullOrEmpty(v)));
        }

        public override string ToString() => ToSummary();

        private static string? Format(string key, object? value)
        {
            return value == null ? null : $"{key}={value}";
        }
    }
}

// <copyright file="LiveProbeResolveStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Models
{
    internal enum LiveProbeResolveStatus
    {
        /// <summary>
        /// The Line Probe was successfully bound to a bytecode offset.
        /// </summary>
        Bound,

        /// <summary>
        /// The Line Probe location did not map out to any loaded module.
        /// The probe will be re-examined whenever a new module is loaded.
        /// </summary>
        Unbound,

        /// <summary>
        /// The probe could not be resolved due to an error.
        /// </summary>
        Error
    }

    internal enum LineProbeResolveReason
    {
        None,
        MissingSourceFile,
        AssemblyNotLoadedOrSymbolsUnavailable,
        LoadedAssemblySourceFileMismatch,
        MissingPdb,
        InvalidLineNumber,
        MissingSequencePoint,
        UnexpectedException
    }

    internal enum LineProbePathMatchType
    {
        ExactSuffixMatch,
        FallbackTrailingSuffixMatch
    }

    internal enum LineProbeFallbackFailureReason
    {
        None,
        NoSameFileNameCandidates,
        NoQualifiedSuffixMatch,
        AmbiguousQualifiedMatches
    }

    internal enum LineProbeDiagnosticLevel
    {
        Minimal,
        Full
    }
}

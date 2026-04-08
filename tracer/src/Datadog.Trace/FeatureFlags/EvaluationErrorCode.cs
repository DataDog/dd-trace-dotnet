// <copyright file="EvaluationErrorCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary>Evaluation error code for error results.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
#if INTERNAL_FFE
internal enum EvaluationErrorCode
#else
public enum EvaluationErrorCode
#endif
{
    /// <summary>No error (not an error result)</summary>
    None,

    /// <summary>Provider not ready (no config loaded)</summary>
    ProviderNotReady,

    /// <summary>Flag not found</summary>
    FlagNotFound,

    /// <summary>Type mismatch</summary>
    TypeMismatch,

    /// <summary>Parse error</summary>
    ParseError,

    /// <summary>Targeting key missing</summary>
    TargetingKeyMissing,

    /// <summary>Invalid context</summary>
    InvalidContext,

    /// <summary>Provider fatal error</summary>
    ProviderFatal,

    /// <summary>General error</summary>
    General
}

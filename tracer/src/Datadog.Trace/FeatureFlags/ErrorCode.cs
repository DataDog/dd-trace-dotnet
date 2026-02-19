// <copyright file="ErrorCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary>Evaluation error codes that map to OpenFeature ErrorType.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
#if INTERNAL_FFE
internal enum ErrorCode
#else
public enum ErrorCode
#endif
{
    /// <summary>No error occurred.</summary>
    None,

    /// <summary>The provider is not ready to evaluate flags.</summary>
    ProviderNotReady,

    /// <summary>The flag was not found.</summary>
    FlagNotFound,

    /// <summary>The flag type does not match the requested type.</summary>
    TypeMismatch,

    /// <summary>An error occurred parsing the flag configuration.</summary>
    ParseError,

    /// <summary>The targeting key is missing from the evaluation context.</summary>
    TargetingKeyMissing,

    /// <summary>The evaluation context is invalid.</summary>
    InvalidContext,

    /// <summary>A general error occurred.</summary>
    General
}

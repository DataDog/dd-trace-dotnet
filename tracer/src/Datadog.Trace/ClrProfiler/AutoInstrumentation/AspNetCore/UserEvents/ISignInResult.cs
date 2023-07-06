// <copyright file="ISignInResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// Duck type of the SignInResult aspnet core type
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISignInResult
{
    /// <summary>
    /// Gets a value indicating whether the sign-in was successful.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a value indicating whether the user attempting to sign-in is locked out.
    /// </summary>
    public bool IsLockedOut { get; }

    /// <summary>
    /// Gets a value indicating whether the user attempting to sign-in is not allowed to sign-in.
    /// </summary>
    public bool IsNotAllowed { get; }
}

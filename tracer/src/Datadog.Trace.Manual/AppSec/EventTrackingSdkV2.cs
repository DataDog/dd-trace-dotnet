// <copyright file="EventTrackingSdkV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Handlers for setting ASM login success / failures events in traces
/// Includes a security scan if asm is enabled
/// </summary>
public static class EventTrackingSdkV2
{
    /// <summary>
    /// Sets the details of a successful login on the local root span
    /// </summary>
    /// <param name="userLogin">The userLogin associated with the login success</param>
    /// <param name="userId">The optional userId associated with the login success</param>
    /// <param name="metadata">The optional metadata associated with the login success</param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TrackUserLoginSuccess(string userLogin, string? userId = null, IDictionary<string, string>? metadata = null)
    {
    }

    /// <summary>
    /// Sets the details of a successful login on the local root span
    /// </summary>
    /// <param name="userLogin">The userLogin associated with the login success</param>
    /// <param name="userDetails">The optional userDetails associated with the login success</param>
    /// <param name="metadata">The optional metadata associated with the login success</param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TrackUserLoginSuccess(string userLogin, UserDetails userDetails, IDictionary<string, string>? metadata = null)
    {
    }

    /// <summary>
    /// Sets the details of a logon failure on the local root span
    /// </summary>
    /// <param name="userLogin">The userLogin associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    /// <param name="userId">The optional userId associated with the login success</param>
    /// <param name="metadata">Metadata associated with the login failure</param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TrackUserLoginFailure(string userLogin, bool exists, string? userId = null, IDictionary<string, string>? metadata = null)
    {
    }

    /// <summary>
    /// Sets the details of a logon failure on the local root span
    /// </summary>
    /// <param name="userLogin">The user login associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    /// <param name="userDetails">The details of the user associated with the login failure</param>
    /// <param name="metadata">Metadata associated with the login failure</param>
    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TrackUserLoginFailure(string userLogin, bool exists, UserDetails userDetails, IDictionary<string, string>? metadata = null)
    {
    }
}

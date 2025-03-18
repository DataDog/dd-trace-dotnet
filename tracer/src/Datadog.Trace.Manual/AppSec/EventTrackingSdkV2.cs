// <copyright file="EventTrackingSdkV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Allow
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
    public static void TrackUserLoginSuccess(string userLogin, string? userId = null, IDictionary<string, string>? metadata = null)
    {
    }
}

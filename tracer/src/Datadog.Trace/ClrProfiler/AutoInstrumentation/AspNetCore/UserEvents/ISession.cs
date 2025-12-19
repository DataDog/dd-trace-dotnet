// <copyright file="ISession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// Duck type of the SignInManager aspnet core type
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal interface ISession
{
    /// <summary>
    /// Gets a value indicating whether the session is available
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the session id
    /// </summary>
    string Id { get; }
}
#endif

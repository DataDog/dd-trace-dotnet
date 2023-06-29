// <copyright file="IIdentityUser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// Duck type of the IdentityUser aspnet core type
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IIdentityUser
{
    /// <summary>
    /// Gets the user id
    /// </summary>
    public object Id { get; }

    /// <summary>
    /// Gets the user email
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the username
    /// </summary>
    public string UserName { get; }
}

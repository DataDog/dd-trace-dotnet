// <copyright file="ISessionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// Duck type of the ISessionFeature aspnet core type in Microsoft.AspNetCore.Http.Features assembly
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
[DuckType("Microsoft.AspNetCore.Session.SessionFeature", "Microsoft.AspNetCore.Session")]
[DuckType("Microsoft.AspNetCore.Http.Features.ISessionFeature", "Microsoft.AspNetCore.Http.Features", IncludeDerivedTypes = true)]
public interface ISessionFeature
{
    /// <summary>
    /// Gets the Session object, can be null
    /// </summary>
    public ISession Session { get; }
}
#endif

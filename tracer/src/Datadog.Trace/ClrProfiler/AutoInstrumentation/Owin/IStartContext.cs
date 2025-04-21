// <copyright file="IStartContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Owin;

/// <summary>
/// Duck Type for Microsoft.Owin.Hosting.Engine.StartContext
/// Interface, as used in generic constraint
/// </summary>
internal interface IStartContext
{
    IAppBuilder Builder { get; }
}

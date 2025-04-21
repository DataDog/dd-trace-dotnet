// <copyright file="IOwinContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Owin;

/// <summary>
/// Duck Type for Microsoft.Owin.Builder.AppBuilder
/// Interface, as used in generic constraint
/// </summary>
internal interface IOwinContext
{
    object Use(object middleware, object[] args);
}

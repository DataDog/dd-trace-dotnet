// <copyright file="IManualSpanContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Proxy for setting the automatic properties on ManualScope
/// </summary>
internal interface IManualSpanContextProxy
{
    void SetAutomatic(object spanContext);
}

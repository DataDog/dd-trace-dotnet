// <copyright file="INullableString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for NullableString
/// Using interface because need to call the setter
/// </summary>
internal interface INullableString
{
    string Value { get; set; }
}
#endif

// <copyright file="IMeter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OTelMetrics.DuckTypes
{
    // Using interface instead of [DuckCopy] struct as we need to set values too
    // TODO: See if we can turn this into a DuckCopy
    internal interface IMeter : IDuckType
    {
        string Name { get; }
    }
}
#endif

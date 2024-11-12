// <copyright file="IPutEventsRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge
{
    /// <summary>
    /// PutEventsRequest interface for ducktyping.
    /// Mirrors Amazon.EventBridge.Model.PutEventsRequest.
    /// </summary>
    internal interface IPutEventsRequest : IDuckType
    {
        ValueWithType<IEnumerable> Entries { get; }
    }
}

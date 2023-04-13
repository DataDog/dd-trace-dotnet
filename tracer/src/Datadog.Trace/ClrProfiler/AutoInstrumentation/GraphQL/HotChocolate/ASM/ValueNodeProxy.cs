// <copyright file="ValueNodeProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM
{
    /// <summary>
    /// ValueNodeProxy struct that contains the value attribute for ducktyping
    /// </summary>
    [DuckCopy]
    internal struct ValueNodeProxy
    {
        public object Value;
    }
}

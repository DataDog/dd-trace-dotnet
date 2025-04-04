// <copyright file="NameStringProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate
{
    /// <summary>
    /// HotChocolate.NameString interface for ducktyping
    /// </summary>
    [DuckCopy]
    internal struct NameStringProxy
    {
        public string Value;
    }

    /// <summary>
    /// nullable structs need an explicit proxy
    /// </summary>
    [DuckCopy]
    internal struct NullableNameStringProxy
    {
        public NameStringProxy Value;
        public bool HasValue;
    }
}

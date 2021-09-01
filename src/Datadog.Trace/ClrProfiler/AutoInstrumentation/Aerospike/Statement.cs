// <copyright file="Statement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike
{
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DuckCopy]
    public struct Statement
    {
        [DuckField(Name = "setName")]
        public string SetName;

        [DuckField(Name = "ns")]
        public string Ns;
    }
}

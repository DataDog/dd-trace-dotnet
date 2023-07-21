// <copyright file="IConnectionString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Ducktyping of Couchbase.ConnectionString
    /// </summary>
    internal interface IConnectionString : IDuckType
    {
        IEnumerable Hosts { get; }
    }
}

// <copyright file="IClientConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.Configuration.Client.ClientConfiguration
/// </summary>
internal interface IClientConfiguration : IDuckType
{
    public IList<Uri> Servers { get; }
}

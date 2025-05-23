// <copyright file="IEndpointDataSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.EndpointsCollection;

internal interface IEndpointDataSource
{
    public IReadOnlyList<object> Endpoints { get; }
}

#endif

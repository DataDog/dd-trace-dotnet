// <copyright file="IHttpMethodMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

namespace Datadog.Trace.AppSec.ApiSec.DuckType;

internal interface IHttpMethodMetadata
{
    public System.Collections.Generic.IReadOnlyList<string> HttpMethods { get; }
}

#endif

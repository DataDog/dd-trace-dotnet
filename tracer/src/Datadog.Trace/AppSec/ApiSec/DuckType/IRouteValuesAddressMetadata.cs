// <copyright file="IRouteValuesAddressMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_2

#nullable enable

namespace Datadog.Trace.AppSec.ApiSec.DuckType;

internal interface IRouteValuesAddressMetadata
{
    public System.Collections.Generic.IReadOnlyDictionary<string,object> RequiredValues { get; }
}

#endif

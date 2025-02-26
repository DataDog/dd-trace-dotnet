// <copyright file="IEndpointMetadataCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_2_OR_GREATER

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.ApiSec.DuckType;

internal interface IEndpointMetadataCollection
{
    [Duck(Name = "GetMetadata", GenericParameterTypeNames = ["Microsoft.AspNetCore.Routing.HttpMethodMetadata, Microsoft.AspNetCore.Routing"])]
    public IHttpMethodMetadata? GetHttpMethodMetadata();

#if NETCOREAPP2_2
    [Duck(Name = "GetMetadata", GenericParameterTypeNames = ["Microsoft.AspNetCore.Routing.IRouteValuesAddressMetadata, Microsoft.AspNetCore.Routing"])]
    public IHttpMethodMetadata? GetRouteValuesAddressMetadata();
#endif
}

#endif

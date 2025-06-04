// <copyright file="IEndpointMetadataCollectionRouteValuesAddressMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.ApiSec.DuckType;

internal interface IEndpointMetadataCollectionRouteValuesAddressMetadata
{
    [Duck(Name = "GetMetadata", GenericParameterTypeNames = ["Microsoft.AspNetCore.Routing.RouteValuesAddressMetadata, Microsoft.AspNetCore.Routing"])]
    public IRouteValuesAddressMetadata? GetRouteValuesAddressMetadata();
}

#endif

// <copyright file="ILegacyAspNetCoreHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Headers
{
    internal interface ILegacyAspNetCoreHeaders : IDuckType
    {
        [Duck(Name = "get_Item", ExplicitInterfaceTypeName = "Microsoft.AspNetCore.Http.IHeaderDictionary", ParameterTypeNames = new[] { "System.String" })]
        object? GetValues(string name);
    }
}

#endif

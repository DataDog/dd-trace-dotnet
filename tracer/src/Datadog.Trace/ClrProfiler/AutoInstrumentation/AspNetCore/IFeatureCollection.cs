// <copyright file="IFeatureCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    internal interface IFeatureCollection
    {
        [Duck(ExplicitInterfaceTypeName = "*")]
        TFeature? Get<TFeature>();

        [Duck(Name = "Get", ExplicitInterfaceTypeName = "*", GenericParameterTypeNames = new[] { "Microsoft.AspNetCore.Routing.IRoutingFeature, Microsoft.AspNetCore.Routing.Abstractions" })]
        object? GetIRoutingFeature();

        [Duck(ExplicitInterfaceTypeName = "*")]
        void Set<TFeature>(TFeature feature);
    }
}
#endif

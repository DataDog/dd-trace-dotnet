// <copyright file="ITypesTuple.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public interface ITypesTuple
    {
        [Duck(Kind = DuckKind.Field)]
        Type ProxyDefinitionType { get; }

        [Duck(Kind = DuckKind.Field)]
        Type TargetType { get; }
    }
}

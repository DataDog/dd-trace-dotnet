// <copyright file="IJToken.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

internal interface IJToken
{
    public JTokenTypeProxy Type { get; }
}

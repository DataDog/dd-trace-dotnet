// <copyright file="IJsonElement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Text.Json;

internal interface IJsonElement
{
    [DuckField(Name = "_parent")]
    object Parent { get; }

    public string GetString();

    public string GetRawText();
}

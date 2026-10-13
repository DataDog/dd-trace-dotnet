// <copyright file="IJValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

[DuckType("Newtonsoft.Json.Linq.JValue", "Newtonsoft.Json", IncludeDerivedTypes = true)]
internal interface IJValue
{
    object Value { get; } // as an interface because in a struct it would fail with an AmbiguousMatchException

    JTokenTypeProxy Type { get; }
}

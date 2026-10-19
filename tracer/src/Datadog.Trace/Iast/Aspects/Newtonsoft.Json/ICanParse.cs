// <copyright file="ICanParse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

[DuckType("Newtonsoft.Json.Linq.JToken", "Newtonsoft.Json")]
[DuckType("Newtonsoft.Json.Linq.JObject", "Newtonsoft.Json")]
[DuckType("Newtonsoft.Json.Linq.JArray", "Newtonsoft.Json")]
internal interface ICanParse
{
    object Parse(string json);
}

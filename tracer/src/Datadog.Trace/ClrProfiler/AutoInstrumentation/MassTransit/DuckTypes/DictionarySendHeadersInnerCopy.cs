// <copyright file="DictionarySendHeadersInnerCopy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// DuckCopy struct targeting DictionarySendHeaders directly.
/// Used in a two-step approach: IMessageSendContext.Headers gives us the DictionarySendHeaders object,
/// then we DuckCopy it here to read its private _headers (IDictionary&lt;string, object&gt;).
/// </summary>
[DuckCopy]
internal struct DictionarySendHeadersInnerCopy
{
    /// <summary>
    /// The underlying headers dictionary from DictionarySendHeaders._headers.
    /// </summary>
    [Duck(Name = "_headers", Kind = DuckKind.Field, BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance)]
    public IDictionary<string, object>? Headers;
}

// <copyright file="ILegacyAspNetCoreHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Headers;

/// <summary>
/// Duck type for IHeaderDictionary
/// </summary>
internal interface ILegacyAspNetCoreHeaders : IDuckType
{
    [Duck(Name = "Item,Microsoft.AspNetCore.Http.IHeaderDictionary.Item", FallbackToBaseTypes = true)]
    IEnumerable<string>? this[string name] { get; }
}

#endif

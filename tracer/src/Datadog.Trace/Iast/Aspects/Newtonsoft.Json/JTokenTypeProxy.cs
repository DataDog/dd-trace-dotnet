// <copyright file="JTokenTypeProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

internal enum JTokenTypeProxy
{
    /// <summary>
    /// No token type has been set.
    /// </summary>
    None = 0,

    /// <summary>
    /// A JSON object.
    /// </summary>
    Object = 1,

    /// <summary>
    /// A JSON array.
    /// </summary>
    Array = 2,

    /// <summary>
    /// A JSON constructor.
    /// </summary>
    Constructor = 3,

    /// <summary>
    /// A JSON object property.
    /// </summary>
    Property = 4,

    /// <summary>
    /// A comment.
    /// </summary>
    Comment = 5,

    /// <summary>
    /// An integer value.
    /// </summary>
    Integer = 6,

    /// <summary>
    /// A float value.
    /// </summary>
    Float = 7,

    /// <summary>
    /// A string value.
    /// </summary>
    String = 8,

    /// <summary>
    /// A boolean value.
    /// </summary>
    Boolean = 9,

    /// <summary>
    /// A null value.
    /// </summary>
    Null = 10,

    /// <summary>
    /// An undefined value.
    /// </summary>
    Undefined = 11,

    /// <summary>
    /// A date value.
    /// </summary>
    Date = 12,

    /// <summary>
    /// A raw JSON value.
    /// </summary>
    Raw = 13,

    /// <summary>
    /// A collection of bytes value.
    /// </summary>
    Bytes = 14,

    /// <summary>
    /// A Guid value.
    /// </summary>
    Guid = 15,

    /// <summary>
    /// A Uri value.
    /// </summary>
    Uri = 16,

    /// <summary>
    /// A TimeSpan value.
    /// </summary>
    TimeSpan = 17
}

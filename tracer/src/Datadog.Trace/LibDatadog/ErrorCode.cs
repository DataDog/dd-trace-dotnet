// <copyright file="ErrorCode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents error codes that can occur when exporting traces.
/// </summary>
internal enum ErrorCode
{
    /// <summary>
    /// Address already in use
    /// </summary>
    AddressInUse = 0,

    /// <summary>
    /// Connection aborted
    /// </summary>
    ConnectionAborted = 1,

    /// <summary>
    /// Connection refused
    /// </summary>
    ConnectionRefused = 2,

    /// <summary>
    /// Connection reset by peer
    /// </summary>
    ConnectionReset = 3,

    /// <summary>
    /// Error parsing HTTP body
    /// </summary>
    HttpBodyFormat = 4,

    /// <summary>
    /// HTTP body too long
    /// </summary>
    HttpBodyTooLong = 5,

    /// <summary>
    /// HTTP error originated by client
    /// </summary>
    HttpClient = 6,

    /// <summary>
    /// Error while parsing HTTP message
    /// </summary>
    HttpParse = 7,

    /// <summary>
    /// HTTP error originated by server
    /// </summary>
    HttpServer = 8,

    /// <summary>
    /// HTTP unknown error
    /// </summary>
    HttpUnknown = 9,

    /// <summary>
    /// HTTP wrong status number
    /// </summary>
    HttpWrongStatus = 10,

    /// <summary>
    /// Invalid argument provided
    /// </summary>
    InvalidArgument = 11,

    /// <summary>
    /// Invalid data payload
    /// </summary>
    InvalidData = 12,

    /// <summary>
    /// Invalid input
    /// </summary>
    InvalidInput = 13,

    /// <summary>
    /// Invalid URL
    /// </summary>
    InvalidUrl = 14,

    /// <summary>
    /// Input/Output error
    /// </summary>
    IoError = 15,

    /// <summary>
    /// Unknown network error
    /// </summary>
    NetworkUnknown = 16,

    /// <summary>
    /// Serialization/Deserialization error
    /// </summary>
    Serde = 17,

    /// <summary>
    /// Operation timed out
    /// </summary>
    TimedOut = 18,
}

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
    AddressInUse = 0,
    ConnectionAborted = 1,
    ConnectionRefused = 2,
    ConnectionReset = 3,
    HttpBodyFormat = 4,
    HttpBodyTooLong = 5,
    HttpClient = 6,
    HttpParse = 7,
    HttpServer = 8,
    HttpUnknown = 9,
    HttpWrongStatus = 10,
    InvalidArgument = 11,
    InvalidData = 12,
    InvalidInput = 13,
    InvalidUrl = 14,
    IoError = 15,
    NetworkUnknown = 16,
    Serde = 17,
    TimedOut = 18,
}

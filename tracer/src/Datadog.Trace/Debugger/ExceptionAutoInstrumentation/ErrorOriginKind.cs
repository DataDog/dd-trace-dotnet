// <copyright file="ErrorOriginKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal enum ErrorOriginKind : byte
    {
        /// <summary>
        /// A first-chance exception is any exception that is initially thrown.
        /// </summary>
        FirstChanceException,

        /// <summary>
        /// A second chance exception is an unhandled exception that has propagated
        /// to the top of the stack and is about to crash the process.
        /// </summary>
        SecondChanceException,

        /// <summary>
        /// An exception that was sent to a logging tool - e.g. via a call to Logger.Error(...)
        /// </summary>
        LoggedError,

        /// <summary>
        /// In ASP.NET / ASP.NET Core / WebAPI etc, an HTTP request failure exception is
        /// an exception in that will cause the request to return a failure response code (usually HTTP 500).
        /// </summary>
        HttpRequestFailure,

        /// <summary>
        /// An exception that was caught by user code.
        /// </summary>
        ExceptionCaught
    }
}

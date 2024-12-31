// <copyright file="IHttpRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Util.DuckTypes
{
    /// <summary>
    /// Ducktype for System.Web.HttpRequest https://referencesource.microsoft.com/#System.Web/HttpRequest.cs,1931
    /// </summary>
    internal interface IHttpRequest
    {
        [DuckField(Name = "_wr")]
        object? WorkerRequest { get; }

        [Duck(Name = "BuildUrl")]
        Uri BuildUrl(Func<string> pathAccessor);
    }
}

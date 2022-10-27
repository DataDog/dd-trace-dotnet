// <copyright file="BeforeRequestStopsArgs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Web;
using Datadog.Trace.Configuration;
#if NETFRAMEWORK
using System.Web.Routing;
#else
using Microsoft.AspNetCore.Http;
#endif

namespace Datadog.Trace.AppSec;

internal class BeforeRequestStopsArgs
{
    private readonly Action<BeforeRequestStopsArgs> _doBeforeBlocking;

    public BeforeRequestStopsArgs(HttpContext httpContext, Scope scope, ImmutableTracerSettings tracerSettings, Action<BeforeRequestStopsArgs> doBeforeBlocking)
    {
        _doBeforeBlocking = doBeforeBlocking;
        HttpContext = httpContext;
        Scope = scope;
        TracerSettings = tracerSettings;
    }

    public HttpContext HttpContext { get; }

    public Scope Scope { get; }

    public ImmutableTracerSettings TracerSettings { get; }

    internal void Invoke() => _doBeforeBlocking?.Invoke(this);
}

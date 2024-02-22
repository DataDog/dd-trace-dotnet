// <copyright file="RaspModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
#if NETFRAMEWORK
using System.Web;
#endif
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RaspModule));

    internal static void OnLfi(string file)
    {
        var arguments = new Dictionary<string, object>() { };
        arguments[AddressesConstants.FileAccess] = file;
        RunWaf(arguments);
    }

    internal static void CheckAndBlock(IResult? result)
    {
        if (result is not null)
        {
            if (result!.ShouldBlock)
            {
                throw new BlockException(result);
            }
        }
    }

    private static void RunWaf(Dictionary<string, object> arguments)
    {
        var security = Security.Instance;
        Log.Information("ENTER FILEEEEE");

        if (!security.RaspEnabled)
        {
            return;
        }

        var rootSpan = Tracer.Instance.InternalActiveScope?.Root?.Span;

        if (rootSpan is null)
        {
            return;
        }

        IResult? result = null;
        SecurityCoordinator? securityCoordinator = null;

#if NETFRAMEWORK
        var context = HttpContext.Current;
        securityCoordinator = new SecurityCoordinator(security, context, rootSpan);
        result = securityCoordinator?.RunWaf(arguments);
#else
        if (CoreHttpContextStore.Instance.Get() is { } httpContext)
        {
            var transport = new SecurityCoordinator.HttpTransport(httpContext);
            if (!transport.IsBlocked)
            {
                securityCoordinator = new SecurityCoordinator(security, httpContext, rootSpan, transport);
                result = securityCoordinator?.RunWaf(arguments);
            }
        }
#endif
        if (result is not null)
        {
            CheckAndBlock(result);
            securityCoordinator?.TryReport(result, false);
            var json = JsonConvert.SerializeObject(result.Data);
            Log.Information("RASP WAF result: {Result}", json);
        }
    }
}

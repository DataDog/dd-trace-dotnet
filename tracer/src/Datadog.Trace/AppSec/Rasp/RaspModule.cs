// <copyright file="RaspModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RaspModule));

    internal static void OnLfi(string file)
    {
        CheckVulnerability(AddressesConstants.FileAccess, file);
    }

    internal static void OnSSRF(string url)
    {
        CheckVulnerability(AddressesConstants.UrlAccess, url);
    }

    private static void CheckVulnerability(string address, string valueToCheck)
    {
        var security = Security.Instance;

        if (!security.RaspEnabled)
        {
            return;
        }

        var rootSpan = Tracer.Instance.InternalActiveScope?.Root?.Span;

        if (rootSpan is null)
        {
            return;
        }

        var arguments = new Dictionary<string, object> { [address] = valueToCheck };
        RunWaf(arguments, rootSpan);
    }

    private static void RunWaf(Dictionary<string, object> arguments, Span rootSpan)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, SecurityCoordinator.Context, rootSpan);
        var result = securityCoordinator.RunWaf(arguments, runWithEphemeral: true);

        if (result?.SendStackInfo != null && Security.Instance.Settings.StackTraceEnabled)
        {
            result.SendStackInfo.TryGetValue("stack_id", out var stackIdObject);
            var stackId = stackIdObject as string;

            if (stackId is null)
            {
                Log.Warning("RASP: A stack was received without Id.");
            }
            else
            {
                SendStack(rootSpan, stackId);
            }
        }

        // we want to report first because if we are inside a try{} catch(Exception ex){} block, we will not report
        // the blockings, so we report first and then block
        securityCoordinator.ReportAndBlock(result);
    }

    private static bool SendStack(Span rootSpan, string id)
    {
        var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth, id);

        if (stack is not null)
        {
            rootSpan.Context.TraceContext.AddStackTraceElement(stack, Security.Instance.Settings.MaxStackTraces);
            return true;
        }

        return false;
    }
}

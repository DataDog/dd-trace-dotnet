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
    private static Dictionary<string, RaspRuleType> _addressRuleType = new()
    {
        { AddressesConstants.FileAccess, RaspRuleType.Lfi }
    };

    internal static void OnLfi(string file)
    {
        CheckVulnerability(AddressesConstants.FileAccess, file);
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
        var result = RunWaf(arguments, rootSpan, address);
    }

    private static void RecordTelemetry(string address, bool isMatch, bool timeOut)
    {
        if (!_addressRuleType.TryGetValue(address, out var ruleType))
        {
            Log.Warning("RASP: Rule type not found for address {Address}", address);
        }

        TelemetryFactory.Metrics.RecordCountRaspRuleEval(ruleType);

        if (isMatch)
        {
            TelemetryFactory.Metrics.RecordCountRaspRuleMatch(ruleType);
        }

        if (timeOut)
        {
            TelemetryFactory.Metrics.RecordCountRaspTimeout(ruleType);
        }
    }

    private static IResult? RunWaf(Dictionary<string, object> arguments, Span rootSpan, string address)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, SecurityCoordinator.Context, Tracer.Instance.InternalActiveScope.Root.Span);
        var result = securityCoordinator.RunWaf(arguments, logException: (log, ex) => log.Error(ex, "Error in RASP."), runWithEphemeral: true);

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

        if (result is not null)
        {
            RecordTelemetry(address, result.ReturnCode == Waf.WafReturnCode.Match, result.Timeout);
        }

        securityCoordinator.CheckAndBlockRasp(result);

        return result;
    }

    private static bool SendStack(Span rootSpan, string id)
    {
        var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth, id);

        if (stack.HasValue)
        {
            rootSpan.Context.TraceContext.AddStackTraceElement(stack.Value, Security.Instance.Settings.MaxStackTraces);
            return true;
        }

        return false;
    }
}

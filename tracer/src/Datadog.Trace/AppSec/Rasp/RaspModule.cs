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

    private static RaspRuleType? TryGetAddressRuleType(string address)
    => address switch
    {
        AddressesConstants.FileAccess => RaspRuleType.Lfi,
        AddressesConstants.UrlAccess => RaspRuleType.Ssrf,
        _ => null,
    };

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
        RunWafRasp(arguments, rootSpan, address);
    }

    private static void RecordRaspTelemetry(string address, bool isMatch, bool timeOut)
    {
        var ruleType = TryGetAddressRuleType(address);

        if (ruleType is null)
        {
            Log.Warning("RASP: Rule type not found for address {Address}", address);
            return;
        }

        TelemetryFactory.Metrics.RecordCountRaspRuleEval(ruleType.Value);

        if (isMatch)
        {
            TelemetryFactory.Metrics.RecordCountRaspRuleMatch(ruleType.Value);
        }

        if (timeOut)
        {
            TelemetryFactory.Metrics.RecordCountRaspTimeout(ruleType.Value);
        }
    }

    private static void RunWafRasp(Dictionary<string, object> arguments, Span rootSpan, string address)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, rootSpan);
        var result = securityCoordinator.RunWaf(arguments, runWithEphemeral: true, isRasp: true);

        if (result is not null)
        {
            RecordRaspTelemetry(address, result.ReturnCode == Waf.WafReturnCode.Match, result.Timeout);
        }

        try
        {
            if (result?.SendStackInfo is not null && Security.Instance.Settings.StackTraceEnabled)
            {
                result.SendStackInfo.TryGetValue("stack_id", out var stackIdObject);
                var stackId = stackIdObject as string;

                if (stackId is null || stackId.Length == 0)
                {
                    Log.Warning("RASP: A stack was received without Id.");
                }
                else
                {
                    SendStack(rootSpan, stackId);
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "RASP: Error while sending stack.");
        }

        AddSpanId(result);

        // we want to report first because if we are inside a try{} catch(Exception ex){} block, we will not report
        // the blockings, so we report first and then block
        securityCoordinator.ReportAndBlock(result);
    }

    private static void AddSpanId(IResult? result)
    {
        if (result?.ReturnCode == WafReturnCode.Match && result?.Data is not null)
        {
            var spanId = Tracer.Instance.InternalActiveScope.Span.SpanId;

            foreach (var item in result.Data)
            {
                // we know that the item is a dictionary because of the way we are deserializing the data
                // Any item contained in the data list comes from the current RASP call, so
                // it should be tagged with current span_id

                if (item is Dictionary<string, object> dictionary)
                {
                    dictionary.Add("span_id", spanId);
                }
            }
        }
    }

    private static void SendStack(Span rootSpan, string id)
    {
        var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth, id);

        if (stack is not null)
        {
            rootSpan.Context.TraceContext.AddStackTraceElement(stack, Security.Instance.Settings.MaxStackTraces);
        }
    }
}

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
        { AddressesConstants.FileAccess, RaspRuleType.Lfi },
        { AddressesConstants.UrlAccess, RaspRuleType.Ssrf }
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
        if (!_addressRuleType.TryGetValue(address, out var ruleType))
        {
            Log.Warning("RASP: Rule type not found for address {Address}", address);
            return;
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

    private static void RunWafRasp(Dictionary<string, object> arguments, Span rootSpan, string address)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, SecurityCoordinator.Context, rootSpan);
        var result = securityCoordinator.RunWaf(arguments, runWithEphemeral: true);

        if (result is not null)
        {
            RecordRaspTelemetry(address, result.ReturnCode == Waf.WafReturnCode.Match, result.Timeout);
        }

        // we want to report first because if we are inside a try{} catch(Exception ex){} block, we will not report
        // the blockings, so we report first and then block
        securityCoordinator.ReportAndBlock(result);
    }
}

// <copyright file="RaspModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspModule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RaspModule));
    private static bool _nullContextReported = false;

    internal enum BlockType
    {
        Irrelevant = 0,
        Success = 1,
        Failure = 2
    }

    private static RaspRuleType? TryGetAddressRuleType(string address)
    => address switch
    {
        AddressesConstants.FileAccess => RaspRuleType.Lfi,
        AddressesConstants.UrlAccess => RaspRuleType.Ssrf,
        AddressesConstants.DBStatement => RaspRuleType.SQlI,
        AddressesConstants.ShellInjection => RaspRuleType.CommandInjectionShell,
        AddressesConstants.CommandInjection => RaspRuleType.CommandInjectionExec,
        _ => null,
    };

    private static RaspRuleTypeMatch? TryGetAddressRuleTypeMatch(string address, BlockType blockType)
    => address switch
    {
        AddressesConstants.FileAccess => blockType switch
        {
            BlockType.Success => RaspRuleTypeMatch.LfiSuccess,
            BlockType.Failure => RaspRuleTypeMatch.LfiFailure,
            BlockType.Irrelevant => RaspRuleTypeMatch.LfiIrrelevant,
            _ => null,
        },
        AddressesConstants.UrlAccess => blockType switch
        {
            BlockType.Success => RaspRuleTypeMatch.SsrfSuccess,
            BlockType.Failure => RaspRuleTypeMatch.SsrfFailure,
            BlockType.Irrelevant => RaspRuleTypeMatch.SsrfIrrelevant,
            _ => null,
        },
        AddressesConstants.DBStatement => blockType switch
        {
            BlockType.Success => RaspRuleTypeMatch.SQlISuccess,
            BlockType.Failure => RaspRuleTypeMatch.SQlIFailure,
            BlockType.Irrelevant => RaspRuleTypeMatch.SQlIIrrelevant,
            _ => null,
        },
        AddressesConstants.ShellInjection => blockType switch
        {
            BlockType.Success => RaspRuleTypeMatch.CommandInjectionShellSuccess,
            BlockType.Failure => RaspRuleTypeMatch.CommandInjectionShellFailure,
            BlockType.Irrelevant => RaspRuleTypeMatch.CommandInjectionShellIrrelevant,
            _ => null,
        },
        AddressesConstants.CommandInjection => blockType switch
        {
            BlockType.Success => RaspRuleTypeMatch.CommandInjectionExecSuccess,
            BlockType.Failure => RaspRuleTypeMatch.CommandInjectionExecFailure,
            BlockType.Irrelevant => RaspRuleTypeMatch.CommandInjectionExecIrrelevant,
            _ => null,
        },
        _ => null,
    };

    internal static void OnLfi(string file)
    {
        CheckVulnerability(new Dictionary<string, object> { [AddressesConstants.FileAccess] = file }, AddressesConstants.FileAccess);
    }

    internal static void OnSSRF(string url)
    {
        CheckVulnerability(new Dictionary<string, object> { [AddressesConstants.UrlAccess] = url }, AddressesConstants.UrlAccess);
    }

    internal static void OnSqlQuery(string sql, IntegrationId id)
    {
        var ddbbType = SqlIntegrationIdToDDBBType(id);
        CheckVulnerability(new Dictionary<string, object> { [AddressesConstants.DBStatement] = sql, [AddressesConstants.DBSystem] = ddbbType }, AddressesConstants.DBStatement);
    }

    private static string SqlIntegrationIdToDDBBType(IntegrationId id)
    {
        // Check https://datadoghq.atlassian.net/wiki/spaces/APM/pages/2357395856/Span+attributes#db.system
        return id switch
        {
            IntegrationId.SqlClient => "sqlserver",
            IntegrationId.MySql => "mysql",
            IntegrationId.Npgsql => "postgresql",
            IntegrationId.Oracle => "oracle",
            IntegrationId.Sqlite => "sqlite",
            IntegrationId.NHibernate => "nhibernate",
            _ => "generic"
        };
    }

    private static void CheckVulnerability(Dictionary<string, object> arguments, string address)
    {
        var security = Security.Instance;

        if (!security.RaspEnabled || !security.AddressEnabled(address))
        {
            return;
        }

        var rootSpan = Tracer.Instance.InternalActiveScope?.Root?.Span as Span;

        if (rootSpan is null || rootSpan.IsFinished || rootSpan.Type != SpanTypes.Web)
        {
            return;
        }

        RunWafRasp(arguments, rootSpan, address);
    }

    private static void RecordRaspTelemetry(string address, bool isMatch, bool timeOut, BlockType matchType)
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
            var ruleTypeMatch = TryGetAddressRuleTypeMatch(address, matchType);

            if (ruleTypeMatch is null)
            {
                Log.Warning("RASP: Rule match type not found for address {Address} {MatchType}", address, matchType);
                return;
            }

            TelemetryFactory.Metrics.RecordCountRaspRuleMatch(ruleTypeMatch.Value);
        }

        if (timeOut)
        {
            TelemetryFactory.Metrics.RecordCountRaspTimeout(ruleType.Value);
        }
    }

    private static void RunWafRasp(Dictionary<string, object> arguments, Span rootSpan, string address)
    {
        var securityCoordinator = SecurityCoordinator.TryGet(Security.Instance, rootSpan);

        // We need a context for RASP
        if (securityCoordinator is null)
        {
            if (!_nullContextReported)
            {
                Log.Warning("Tried to run Rasp but security coordinator couldn't be instantiated, probably because of httpcontext missing");
                _nullContextReported = true;
            }
            else
            {
                Log.Debug("Tried to run Rasp but security coordinator couldn't be instantiated, probably because of httpcontext missing");
            }

            return;
        }

        var result = securityCoordinator.Value.RunWaf(arguments, runWithEphemeral: true, isRasp: true);

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
        catch (Exception ex)
        {
            Log.Error(ex, "RASP: Error while sending stack.");
        }

        AddSpanId(result);

        if (result is not null)
        {
            // we want to report first because if we are inside a try{} catch(Exception ex){} block, we will not report
            // the blockings, so we report first and then block
            try
            {
                var matchSuccesCode = result.ReturnCode == WafReturnCode.Match && result.ShouldBlock ?
                    BlockType.Success : BlockType.Irrelevant;

                securityCoordinator.Value.ReportAndBlock(result, () => RecordRaspTelemetry(address, result.ReturnCode == Waf.WafReturnCode.Match, result.Timeout, matchSuccesCode));
            }
            catch (Exception ex) when (ex is not BlockException)
            {
                var matchFailureCode = result.ReturnCode == WafReturnCode.Match && result.ShouldBlock ?
                    BlockType.Failure : BlockType.Irrelevant;

                RecordRaspTelemetry(address, result.ReturnCode == Waf.WafReturnCode.Match, result.Timeout, matchFailureCode);
                Log.Error(ex, "RASP: Error while reporting and blocking.");
            }
        }
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
        var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth, Security.Instance.Settings.MaxStackTraceDepthTopPercent, id);

        if (stack is not null)
        {
            rootSpan.Context.TraceContext.AppSecRequestContext.AddRaspStackTrace(stack, Security.Instance.Settings.MaxStackTraces);
        }
    }

    internal static void OnCommandInjection(string fileName, string argumentLine, Collection<string>? argumentList, bool useShellExecute)
    {
        try
        {
            if (!Security.Instance.RaspEnabled)
            {
                return;
            }

            if (useShellExecute)
            {
                var commandLine = RaspShellInjectionHelper.BuildCommandInjectionCommand(fileName, argumentLine, argumentList);

                if (!string.IsNullOrEmpty(commandLine))
                {
                    CheckVulnerability(new Dictionary<string, object> { [AddressesConstants.ShellInjection] = commandLine }, AddressesConstants.ShellInjection);
                }
            }
            else
            {
                var commandLine = RaspShellInjectionHelper.BuildCommandInjectionCommandArray(fileName, argumentLine, argumentList);

                if (commandLine is not null)
                {
                    CheckVulnerability(new Dictionary<string, object> { [AddressesConstants.CommandInjection] = commandLine }, AddressesConstants.CommandInjection);
                }
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            Log.Error(ex, "RASP: Error while checking command injection.");
        }
    }
}

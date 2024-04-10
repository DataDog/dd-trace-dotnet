// <copyright file="RaspModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspModule
{
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
        RunWaf(arguments, rootSpan);
    }

    private static void RunWaf(Dictionary<string, object> arguments, Span rootSpan)
    {
        var securityCoordinator = new SecurityCoordinator(Security.Instance, SecurityCoordinator.Context, Tracer.Instance.InternalActiveScope.Root.Span);
        var result = securityCoordinator.RunWaf(arguments, (log, ex) => log.Error(ex, "Error in RASP."), true);

        if (result?.ShouldSendStack == true && Security.Instance.Settings.StackTraceEnabled)
        {
            // TODO: Right now, the WAF does not generate a stack_id, but it will in the future, so
            // we are creating a stack id and adding it to the result as a temporary solution.
            // This code will be removed when the WAF generates the stack id.
            var stackId = SendStack(rootSpan);

            if (!string.IsNullOrEmpty(stackId))
            {
                AddStackIdToResult(stackId!, result);
            }
        }

        securityCoordinator.CheckAndBlockRasp(result);
    }

    private static void AddStackIdToResult(string stackId, IResult result)
    {
        var data = result.Data as List<object>;

        if (data is not null)
        {
            foreach (var item in data)
            {
                if (item is Dictionary<string, object> dictionary)
                {
                    dictionary["stack_id"] = stackId;
                }
            }
        }
    }

    private static string? SendStack(Span rootSpan)
    {
        var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth);

        if (stack.HasValue)
        {
            rootSpan.Context.TraceContext.AddStackTraceElement(stack.Value, Security.Instance.Settings.MaxStackTraces);
            return stack.Value.Id;
        }

        return null;
    }
}

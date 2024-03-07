// <copyright file="RaspModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Rasp;

internal static class RaspModule
{
    internal static void OnLfi(string file)
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

        var arguments = new Dictionary<string, object> { [AddressesConstants.FileAccess] = file };
        RunWaf(arguments);
    }

    private static void RunWaf(Dictionary<string, object> arguments)
    {
        if (Tracer.Instance.InternalActiveScope?.Root?.Span != null)
        {
            var securityCoordinator = new SecurityCoordinator(Security.Instance, SecurityCoordinator.Context, Tracer.Instance.InternalActiveScope.Root.Span);
            var result = securityCoordinator.RunWaf(arguments, (log, ex) => log.Error(ex, "Error in RASP OnLfi"));
            securityCoordinator.CheckAndBlock(result);
        }
    }
}

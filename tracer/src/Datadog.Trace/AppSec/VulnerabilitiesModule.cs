// <copyright file="VulnerabilitiesModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using System.Diagnostics;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;

#nullable enable

namespace Datadog.Trace.AppSec;

internal static class VulnerabilitiesModule
{
    internal static void OnPathTraversal(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            IastModule.OnPathTraversal(path);
            RaspModule.OnLfi(path);
        }
    }

    internal static void OnSSRF(string uriText)
    {
        if (!string.IsNullOrEmpty(uriText))
        {
            IastModule.OnSSRF(uriText);
            RaspModule.OnSSRF(uriText);
        }
    }

    internal static void OnSqlQuery(string query, IntegrationId integrationId)
    {
        if (!string.IsNullOrEmpty(query))
        {
            IastModule.OnSqlQuery(query, integrationId);
            RaspModule.OnSqlQuery(query, integrationId);
        }
    }

    internal static void OnCommandInjection(ProcessStartInfo startInfo)
    {
#if NETCOREAPP3_1_OR_GREATER
        IastModule.OnCommandInjection(startInfo.FileName, startInfo.Arguments, startInfo.ArgumentList, Configuration.IntegrationId.Process);
        RaspModule.OnCommandInjection(startInfo.FileName, startInfo.Arguments, startInfo.ArgumentList, startInfo.UseShellExecute);
#else
        IastModule.OnCommandInjection(startInfo.FileName, startInfo.Arguments, null, Configuration.IntegrationId.Process);
        RaspModule.OnCommandInjection(startInfo.FileName, startInfo.Arguments, null, startInfo.UseShellExecute);
#endif
    }
}

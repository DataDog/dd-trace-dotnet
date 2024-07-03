// <copyright file="VulnerabilitiesModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

    internal static void OnSqlI(string query, IntegrationId integrationId)
    {
        if (!string.IsNullOrEmpty(query))
        {
            IastModule.OnSqlQuery(query, integrationId);
            RaspModule.OnSqlI(query, integrationId);
        }
    }
}

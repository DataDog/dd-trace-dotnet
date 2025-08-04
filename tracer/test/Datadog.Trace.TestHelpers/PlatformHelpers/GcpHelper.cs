// <copyright file="GcpHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.TestHelpers.PlatformHelpers;

public static class GcpHelper
{
    public static IConfigurationSource CreateMinimalCloudRunFunctionsConfiguration(string functionTarget, string kService)
    {
        var dict = new Dictionary<string, string>
        {
            { "FUNCTION_TARGET", functionTarget },
            { "K_SERVICE", kService },
        };

        return new DictionaryConfigurationSource(dict);
    }

    public static IConfigurationSource CreateMinimalFirstGenCloudRunFunctionsConfiguration(string functionName, string gcpProject)
    {
        var dict = new Dictionary<string, string>
        {
            { "FUNCTION_NAME", functionName },
            { "GCP_PROJECT", gcpProject },
        };

        return new DictionaryConfigurationSource(dict);
    }
}

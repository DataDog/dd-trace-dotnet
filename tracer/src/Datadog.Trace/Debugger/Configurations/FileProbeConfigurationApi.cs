// <copyright file="FileProbeConfigurationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Configurations;

internal class FileProbeConfigurationApi : IProbeConfigurationApi
{
    private readonly string _targetPath;

    private FileProbeConfigurationApi(string targetPath)
    {
        _targetPath = targetPath;
    }

    public static FileProbeConfigurationApi Create(ImmutableDebuggerSettings debuggerSettings)
    {
        return new FileProbeConfigurationApi(debuggerSettings.ProbeConfigurationsPath);
    }

    public Task<ProbeConfiguration> GetConfigurationsAsync()
    {
        var content = File.ReadAllText(_targetPath);
        var config = JsonConvert.DeserializeObject<ProbeConfiguration>(content);

        return Task.FromResult(config);
    }
}

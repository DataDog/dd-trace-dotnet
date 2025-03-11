// <copyright file="FleetInstallerLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public class FleetInstallerLogger(ITestOutputHelper output) : Datadog.FleetInstaller.ILogger
{
    private readonly ITestOutputHelper _output = output;

    public void WriteInfo(string message) => _output.WriteLine("INFO: " + message);

    public void WriteError(string message) => _output.WriteLine("ERROR: " + message);

    public void WriteWarning(string message) => _output.WriteLine("WARNING: " + message);

    public void WriteError(Exception ex, string message)
    {
        _output.WriteLine("ERROR: " + message + Environment.NewLine + ex);
    }
}

#endif

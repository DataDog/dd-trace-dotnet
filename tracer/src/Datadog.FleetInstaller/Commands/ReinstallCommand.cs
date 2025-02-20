// <copyright file="ReinstallCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.FleetInstaller.Commands;

// For now, reinstall = install, but we have the option to split later without changing the public API
internal class ReinstallCommand : InstallCommand
{
    public ReinstallCommand()
        : base("reinstall")
    {
    }
}

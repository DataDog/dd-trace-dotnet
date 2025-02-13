// <copyright file="CommandBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

internal abstract class CommandBase : Command
{
    protected CommandBase(string name, string? description = null)
        : base(name, description)
    {
    }

    protected bool IsValidEnvironment(CommandResult commandResult)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            commandResult.ErrorMessage = $"This installer is only intended to run on Windows, it cannot be used on {RuntimeInformation.OSDescription}";
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            commandResult.ErrorMessage = $"This installer must be run with administrator privileges. Current user {identity.Name} is not an administrator.";
            return false;
        }

        if (!RegistryHelper.TryGetIisVersion(Log.Instance, out var version))
        {
            commandResult.ErrorMessage = "This installer requires IIS 10.0 or later. Could not determine the IIS version; is the IIS feature enabled?";
            return false;
        }

        if (version.Major < 10)
        {
            commandResult.ErrorMessage = $"This installer requires IIS 10.0 or later. Detected IIS version {version.Major}.{version.Minor}";
            return false;
        }

        return true;
    }
}

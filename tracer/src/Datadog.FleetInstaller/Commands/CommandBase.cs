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
    private readonly Option<string> _versionedPathOption = new("--home-path", () => null!)
    {
        Description = "Path to the tracer-home-directory",
        IsRequired = true
    };

    protected CommandBase(string command)
        : base(command)
    {
        AddOption(_versionedPathOption);

        AddValidator(Validate);

        this.SetHandler(ExecuteAsync);
    }

    public async Task ExecuteAsync(InvocationContext context)
    {
        var versionedPath = context.ParseResult.GetValueForOption(_versionedPathOption)!;
        var tracerValues = new TracerValues(versionedPath);
        var log = Log.Instance;

        var result = await ExecuteAsync(log, context, tracerValues).ConfigureAwait(false);

        context.ExitCode = (int)result;
    }

    protected abstract Task<ReturnCode> ExecuteAsync(ILogger log, InvocationContext context, TracerValues versionedPath);

    private void Validate(CommandResult commandResult)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            commandResult.ErrorMessage = $"This installer is only intended to run on Windows, it cannot be used on {RuntimeInformation.OSDescription}";
            return;
        }

        using (var identity = WindowsIdentity.GetCurrent())
        {
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                commandResult.ErrorMessage = $"This installer must be run with administrator privileges. Current user {identity.Name} is not an administrator.";
                return;
            }
        }

        var path = commandResult.GetValueForOption(_versionedPathOption);
        if (path is not null && !FileHelper.TryVerifyFiles(Log.Instance, new TracerValues(path), out var err))
        {
            commandResult.ErrorMessage = err;
        }
    }
}

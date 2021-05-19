using System;
using Microsoft.Build.Tasks;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;

[Serializable]
public class DotNetMSBuildSettings : MSBuildSettings
{
    /// <summary>
    ///   Path to the DotNet executable.
    /// </summary>
    public override string ProcessToolPath => DotNetTasks.DotNetPath;
    public override Action<OutputType, string> ProcessCustomLogger => DotNetTasks.DotNetLogger;
    protected override Arguments ConfigureProcessArguments(Arguments arguments)
    {
        arguments
            .Add("msbuild");
        return base.ConfigureProcessArguments(arguments);
    }
}

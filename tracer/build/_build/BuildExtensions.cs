using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

public static class BuildExtensions
{
    public static DotNetTestSettings WithMemoryDumpAfter(this DotNetTestSettings settings, int timeoutInMinutes)
    {
        return settings.SetProcessArgumentConfigurator(
            args =>
                args.Add("--blame-hang")
                    .Add("--blame-hang-dump-type full")
                    .Add($"--blame-hang-timeout {timeoutInMinutes}m")
            );
    }
}

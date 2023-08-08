using System.Threading.Tasks;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

partial class Build
{
    private Task RenameRepositoryForCiApp()
    {
        // Remove test projects from solution
        TestsDirectory
            .GlobFiles("**/*.csproj")
            .ForEach(path => DotNetTasks.DotNet(
                $"sln {Solution.FileName} remove {path}",
                workingDirectory: RootDirectory));

        // Delete test code
        FileSystemTasks.DeleteDirectory(TestsDirectory);

        // Rename all Datadog.Tracer.Native references and files
        // Rename all Datadog.Trace references and files
        // Change all the DD_ variables to something that doesn't conflict (DDCI_
        // Change the profiling Guid to a different one to avoid any risks
        // Rename the dd-trace tool to dd-ci-internal or something
        // Update new public key here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/dd_profiler_constants.h#L122-L130
        return Task.CompletedTask;
    }
}

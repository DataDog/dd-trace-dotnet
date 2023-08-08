using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

partial class Build
{
    private Task RenameRepositoryForCiApp()
    {
        const string oldNativeName = "Datadog.Tracer.Native";
        const string newNativeName = "DatadogCiApp.Native";

        const string oldDatadogTraceName = "Datadog.Trace";
        const string newDatadogTraceName = "DatadogCiApp";

        const string oldDdTraceName = "dd-trace";
        const string newDdTraceName = "dd-trace-ciapp";

        // Remove test projects from solution
        TestsDirectory
            .GlobFiles("**/*.csproj")
            .ForEach(path => DotNetTasks.DotNet(
                $"sln {Solution.FileName} remove {path}",
                workingDirectory: RootDirectory));

        // Delete test code
        FileSystemTasks.DeleteDirectory(TestsDirectory);

        // rename directories
        var toRename = new List<string>();
        do
        {
            toRename.Clear();
            foreach (var path in Directory.EnumerateDirectories(RootDirectory, "*", SearchOption.AllDirectories))
            {
                if (path.Contains(oldDatadogTraceName) || path.Contains(oldNativeName) || path.Contains(oldDdTraceName))
                {
                    toRename.Add(path);
                }
            }

            foreach (var dir in toRename)
            {
                var newDir = dir
                    .Replace(oldNativeName, newNativeName)
                    .Replace(oldDatadogTraceName, newDatadogTraceName)
                    .Replace(oldDdTraceName, newDdTraceName);
                FileSystemTasks.EnsureExistingParentDirectory(newDir);
                try
                {
                    FileSystemTasks.RenameDirectory(dir, newDir, DirectoryExistsPolicy.Merge);
                }
                catch (DirectoryNotFoundException)
                {
                    Log.Information("Failed to move {Dir} - directory does not exist", dir);
                }
            }
        } while (toRename.Count > 0); // do the loop to catch cases where we moved stuff

        // Find files to rename and replace known strings
        toRename.Clear();
        var sb = new StringBuilder(5000);
        foreach (var file in Directory.GetFiles(RootDirectory, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file) is ".cs" or ".csproj" or ".vcxproj" or ".sln" or "props" or ".conf" or ".h" or ".cpp" or ".slnf" or ".snk" or ".yml" or ".json")
            {
                var filename = Path.GetFileName(file);
                if (filename.Contains(oldDatadogTraceName) || filename.Contains(oldNativeName) || filename.Contains(oldDdTraceName))
                {
                    toRename.Add(file);
                }

                if (file == Solution.Path / "tracer" / "build" / "_build" / "CiVisibilityRename" / "Build.CiVisibilityRenamer.cs")
                {
                    // don't replace this file (things get confusing)
                    continue;
                }

                ReplaceFile(sb, file);
            }
        }

        // rename files
        foreach (var path in toRename)
        {
            var newName = Path.GetFileName(path)
                .Replace(oldNativeName, newNativeName)
                .Replace(oldDatadogTraceName, newDatadogTraceName)
                .Replace(oldDdTraceName, newDdTraceName);
            FileSystemTasks.RenameFile(path, newName);
        }

        // Update new public key here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/dd_profiler_constants.h#L122-L130
        return Task.CompletedTask;

        static void ReplaceFile(StringBuilder sb, string filename)
        {
            // this creates a tonne of garbage, but is the easiest way to do it
            var contents = File.ReadAllText(filename);
            sb.Clear();
            sb.Append(contents);

            // Native library
            sb.Replace(oldNativeName, newNativeName);

            // "Main" library
            sb.Replace(oldDatadogTraceName, newDatadogTraceName);

            // Environment variables
            sb.Replace("\"DD_", "\"DDCIAPP_");

            // Profiler guid
            sb.Replace("846F5F1C-F9AE-4B07-969E-05C26BC060D8", "0DB16C43-C20F-4677-BE66-D430D19F8DD3");

            // repo references (for safety)
            sb.Replace("dd-trace-dotnet", "dd-trace-internalciapp");

            // dd-trace tool
            sb.Replace(oldDdTraceName, newDdTraceName);

            var final = sb.ToString();
            if (final.Equals(contents, StringComparison.Ordinal))
            {
                return;
            }

            Log.Information("Replacing contents of {Filename}", filename);
            File.WriteAllText(filename, final);
        }
    }
}

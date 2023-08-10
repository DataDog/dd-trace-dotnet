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
        const string newNativeName = "DatadogCiApp.Tracer.Native";

        const string oldDatadogTraceName = "Datadog.Trace";
        const string newDatadogTraceName = "DatadogCiApp.Trace";

        const string oldUsing = "using Datadog;";
        const string newUsing = "using DatadogCiApp;";

        const string oldDdTraceName = "dd-trace";
        const string newDdTraceName = "dd-trace-ciapp";

        // Remove test projects from solution
        TestsDirectory
            .GlobFiles("**/*.csproj", "**/*.vcxproj", "**/*.vbproj", "**/*.shproj")
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
            if (Path.GetExtension(file) is ".cs" or ".csproj" or ".vcxproj" or ".sln" or ".props" or ".conf" or ".def" or ".h" or ".cpp" or ".slnf" or ".snk" or ".yml" or ".json" or ".proj" or ".xml" or ".rc" or ".gitignore" or ".dockerignore" or ".txt" or ".targets")
            {
                var filename = Path.GetFileName(file);
                if (filename.Contains(oldDatadogTraceName) || filename.Contains(oldNativeName) || filename.Contains(oldDdTraceName))
                {
                    toRename.Add(file);
                }

                if (Path.GetFileName(file).Equals("Build.CiVisibilityRenamer.cs", StringComparison.OrdinalIgnoreCase))
                {
                    // don't replace this file (things get too confusing)
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

            // strip out the tests from the cmakelists
            if (Path.GetFileName(filename).Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase))
            {
                sb.Replace("add_subdirectory(test)", string.Empty);
                sb.Replace("Datadog.Tracer.Native.Tests", string.Empty);
            }

            // Native library
            sb.Replace(oldNativeName, newNativeName);

            // "Main" library
            sb.Replace(oldDatadogTraceName, newDatadogTraceName);

            // using statements
            sb.Replace(oldUsing, newUsing);

            // Environment variables
            sb.Replace("\"DD_", "\"DDCIAPP_");

            // Native profiler guid
            sb.Replace("846F5F1C-F9AE-4B07-969E-05C26BC060D8", "0DB16C43-C20F-4677-BE66-D430D19F8DD3");
            sb.Replace("{0x846f5f1c, 0xf9ae, 0x4b07, {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8}}", "{0x0db16c43, 0xc20f, 0x4677, {0xbe, 0x66, 0xd4, 0x30, 0xd1, 0x9f, 0x8d, 0xd3}};");

            // Native tracer guid
            sb.Replace("50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5", "74988B26-836B-4D6E-87DD-96AD50DD5053");
            sb.Replace("{0x50da5eed, 0xf1ed, 0xb00b, {0x10, 0x55, 0x5a, 0xfe, 0x55, 0xa1, 0xad, 0xe5}}", "{0x74988b26, 0x836b, 0x4d6e, {0x87, 0xdd, 0x96, 0xad, 0x50, 0xdd, 0x50, 0x53}};");

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

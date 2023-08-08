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

        // Find files to rename and replace known strings
        var filesToRename = new List<string>();
        var sb = new StringBuilder(5000);
        foreach (var file in Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file) is ".cs" or ".csproj" or ".vcxproj" or ".sln" or "props" or ".conf" or ".h" or ".cpp")
            {
                var filename = Path.GetFileName(file);
                if (filename.Contains(oldDatadogTraceName) || filename.Contains(oldNativeName) || filename.Contains(oldDdTraceName))
                {
                    filesToRename.Add(file);
                }

                ReplaceFile(sb, file);
            }
        }

        // rename files
        foreach (var path in filesToRename)
        {
            var newName = Path.GetFileName(path)
                .Replace(oldNativeName, newNativeName)
                .Replace(oldDatadogTraceName, newDatadogTraceName)
                .Replace(oldDdTraceName, newDdTraceName);
            FileSystemTasks.RenameFile(path, newName);
        }

        // rename directories
        RenameDirectory(oldNativeName, newNativeName);
        RenameDirectory(oldDatadogTraceName, newDatadogTraceName);
        RenameDirectory(oldDdTraceName, newDdTraceName);

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
            File.WriteAllText(filename, contents);
        }

        void RenameDirectory(string oldName, string newName)
        {
            foreach (var dir in Directory.GetDirectories(RootDirectory, $"*{oldName}*", SearchOption.AllDirectories))
            {
                var newDir = dir.Replace(oldName, newName);
                FileSystemTasks.EnsureExistingParentDirectory(newDir);
                FileSystemTasks.RenameDirectory(dir, newDir);
            }
        }
    }
}

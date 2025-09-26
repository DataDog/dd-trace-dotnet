using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;

using Logger = Serilog.Log;

internal static class MSBuildLogHelper
{
    static internal AbsolutePath MsbuildDebugPath => NukeBuild.RootDirectory / "logs" / "msbuild";

    internal static void DumpMsBuildChildFailures(int tailChars = 10 * 1024, int maxFiles = 2)
    {
        try
        {
            var msbuildDebugPath = MsbuildDebugPath.ToString();
            if (!Directory.Exists(msbuildDebugPath))
            {
                Logger.Information($"No MSBuild failure directory: {msbuildDebugPath}");
                return;
            }

            var files = Directory.EnumerateFiles(msbuildDebugPath, "MSBuild_*.failure.txt", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(maxFiles)
                .ToList();

            if (files.Count == 0)
            {
                Logger.Information("No MSBuild failure notes found.");
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    // Use BOM detection: handles UTF-8/UTF-16 automatically
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    var text = sr.ReadToEnd();

                    var tail = text.Length > tailChars ? text[^tailChars..] : text;

                    Logger.Error($"----- BEGIN {file} (showing last {tail.Length} of {text.Length} chars) -----");
                    Logger.Error(tail);
                    Logger.Error("----- END -----");
                }
                catch (Exception exRead)
                {
                    Logger.Warning($"Could not read {file}: {exRead.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to dump MSBuild child-node diagnostics: {ex.Message}");
        }
    }
}

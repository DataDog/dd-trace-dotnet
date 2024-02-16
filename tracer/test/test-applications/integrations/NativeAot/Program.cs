using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace;
using Microsoft.Build.Framework;
using Mono.Cecil.Cil;

namespace NativeAot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var tempFile = Path.GetTempFileName();
            //var tempFolder = tempFile.Replace(".tmp", string.Empty);

            var tempFolder = @"C:\temp\nativeaot";

            //var directory = Directory.CreateDirectory(tempFolder);
            //CopyDirectory($@"C:\Users\kevin.gosse\source\repos\NativeAotTest\NativeAotTest\obj", Path.Combine(tempFolder, "obj"), true);
            CopyDirectory(@"C:\Users\kevin.gosse\source\repos\NativeAotTest\NativeAotTest\bin\Release\net8.0\win-x64", tempFolder, true);

            // Add the latest Datadog.Trace.dll
            File.Copy(typeof(Tracer).Assembly.Location, Path.Combine(tempFolder, "Datadog.Trace.dll"), true);

            var oldCurrentDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = tempFolder;

            var paths = new List<string>();

            foreach (var file in Directory.GetFiles(tempFolder, "*.dll", SearchOption.AllDirectories))
            {
                paths.Add(file);
            }

            paths.Sort((x, y) =>
            {
                if (x.Contains("NativeAotTest.dll"))
                {
                    return -1;
                }
                else if (y.Contains("NativeAotTest.dll"))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            });

            // Patch rsp file to point to local Datadog.Trace.dll (to make it easier to use the latest)
            //var rspFile = File.ReadAllLines(@"obj\Release\net8.0\win-x64\native\NativeAotTest.ilc.rsp");

            //for (int i = 0; i < rspFile.Length; i++)
            //{
            //    ref var line = ref rspFile[i];

            //    line = line.Replace(@"\datadog\", @"\");

            //    if (line.StartsWith("-r:") && line.EndsWith("Datadog.Trace.dll"))
            //    {
            //        var path = typeof(Tracer).Assembly.Location;
            //        var newPath = Path.Combine(tempFolder, Path.GetFileName(path));

            //        File.Copy(path, newPath, true);

            //        line = $"-r:{newPath}";
            //    }
            //}

            //File.WriteAllLines(@"obj\Release\net8.0\win-x64\native\NativeAotTest.ilc.rsp", rspFile);

            Datadog.Trace.NativeAotTask.AotProcessor.Invoke(paths, Console.WriteLine);

            //try
            //{

            //    //var task = new Datadog.Trace.NativeAotTask.InjectDatadog();
            //    //task.BuildEngine = new FakeBuildEngine();
            //    //task.IlcRspFile = @"obj\Release\net8.0\win-x64\native\NativeAotTest.ilc.rsp";
            //    //task.IntermediateOutputPath = @"obj\Release\net8.0\win-x64\";
            //    //task.Execute();
            //}
            //finally
            //{
            //    // Restore the old current directory, otherwise we won't be able to delete the temp folder
            //    Environment.CurrentDirectory = oldCurrentDirectory;
            //    directory.Delete(true);
            //    File.Delete(tempFile);
            //}
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            var dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (var file in dir.GetFiles())
            {
                var targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (var subDir in dirs)
                {
                    var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }

    public class FakeBuildEngine : IBuildEngine
    {
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Console.WriteLine($"[Error] {e.Message}");
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Console.WriteLine($"[Warning] {e.Message}");
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Console.WriteLine($"[Info] {e.Message}");
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            Console.WriteLine($"[Custom event] {e.Message}");
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public bool ContinueOnError => true;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;
    }
}

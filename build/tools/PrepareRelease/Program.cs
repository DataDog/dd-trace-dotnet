// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PrepareRelease
{
    public class Program
    {
        public const string Versions = "versions";
        public const string Integrations = "integrations";
        public const string Msi = "msi";

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException($@"You must specify at least one job name from [""{Versions}"", ""{Integrations}, ""{Msi}""].");
            }

            var solutionDir = GetSolutionDirectory();

            if (JobShouldRun(Integrations, args))
            {
                Console.WriteLine("--------------- Integrations Job Started ---------------");
                GenerateIntegrationDefinitions.Run(solutionDir);
                Console.WriteLine("--------------- Integrations Job Complete ---------------");
            }

            if (JobShouldRun(Versions, args))
            {
                Console.WriteLine("--------------- Versions Job Started ---------------");
                new SetAllVersions(solutionDir).Run();
                Console.WriteLine("--------------- Versions Job Complete ---------------");
            }

            if (JobShouldRun(Msi, args))
            {
                Environment.SetEnvironmentVariable("SOLUTION_DIR", solutionDir);

                var outputDir = Path.Combine(solutionDir, "bin", "tracer-home");
                var publishBatch = Path.Combine(solutionDir, "build", "tools", "PrepareRelease", "publish-all.bat");
                ExecuteCommand(publishBatch);

                Console.WriteLine("--------------- MSI Job Started ---------------");
                SyncMsiContent.Run(solutionDir, outputDir);
                Console.WriteLine("--------------- MSI Job Complete ---------------");
            }
        }

        private static bool JobShouldRun(string jobName, string[] args)
        {
            return args.Any(a => string.Equals(a, jobName, StringComparison.OrdinalIgnoreCase));
        }

        private static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command) { CreateNoWindow = true, UseShellExecute = true };
            var process = Process.Start(processInfo);
            process.WaitForExit(120_000);
            Console.WriteLine("Publish ExitCode: " + process.ExitCode, "ExecuteCommand");
            process?.Close();
        }

        private static string GetSolutionDirectory()
        {
            var startDirectory = Environment.CurrentDirectory;
            var currentDirectory = Directory.GetParent(startDirectory);
            const string searchItem = @"Datadog.Trace.sln";

            while (true)
            {
                var slnFile = currentDirectory.GetFiles(searchItem).SingleOrDefault();

                if (slnFile != null)
                {
                    break;
                }

                currentDirectory = currentDirectory.Parent;

                if (currentDirectory == null || !currentDirectory.Exists)
                {
                    throw new Exception($"Unable to find solution directory from: {startDirectory}");
                }
            }

            return currentDirectory.FullName;
        }
    }
}

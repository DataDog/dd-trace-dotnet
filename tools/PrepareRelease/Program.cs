using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Core.Tools;

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
                throw new ArgumentException("You must specify at least one job name");
            }

            var solutionDir = EnvironmentTools.GetSolutionDirectory();

            if (JobShouldRun(Integrations, args))
            {
                Console.WriteLine("--------------- Integrations Job Started ---------------");
                GenerateIntegrationDefinitions.Run(solutionDir);
                Console.WriteLine("--------------- Integrations Job Complete ---------------");
            }

            if (JobShouldRun(Versions, args))
            {
                Console.WriteLine("--------------- Versions Job Started ---------------");
                SetAllVersions.Run();
                Console.WriteLine("--------------- Versions Job Complete ---------------");
            }

            if (JobShouldRun(Msi, args))
            {
                Environment.SetEnvironmentVariable("SOLUTION_DIR", solutionDir);
                var publishBatch = Path.Combine(solutionDir, "tools", "PrepareRelease", "publish-all.bat");
                ExecuteCommand(publishBatch);

                Console.WriteLine("--------------- MSI Job Started ---------------");
                SyncMsiContent.Run();
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
    }
}

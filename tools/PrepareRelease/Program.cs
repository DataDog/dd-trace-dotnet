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
            if (JobShouldRun(Versions, args))
            {
                Console.WriteLine("--------------- Versions Job Started ---------------");
                SetAllVersions.Run();
                Console.WriteLine("--------------- Versions Job Complete ---------------");
            }

            var solutionDir = EnvironmentTools.GetSolutionDirectory();
            Environment.SetEnvironmentVariable("SOLUTION_DIR", solutionDir);
            var tracerHomeOutput = Path.Combine(solutionDir, "tools", "PrepareRelease", "bin", "tracer-home");
            Environment.SetEnvironmentVariable("TRACER_HOME_OUTPUT_DIR", tracerHomeOutput);

            var publishBatch = Path.Combine(solutionDir, "tools", "PrepareRelease", "publish-all.bat");
            ExecuteCommand(publishBatch);

            if (JobShouldRun(Integrations, args))
            {
                Console.WriteLine("--------------- Integrations Job Started ---------------");
                GenerateIntegrationDefinitions.Run(solutionDir, tracerHomeOutput);
                Console.WriteLine("--------------- Integrations Job Complete ---------------");
            }

            if (JobShouldRun(Msi, args))
            {
                Console.WriteLine("--------------- MSI Job Started ---------------");
                SyncMsiContent.Run();
                Console.WriteLine("--------------- MSI Job Complete ---------------");
            }
        }

        private static bool JobShouldRun(string jobName, string[] args)
        {
            return args.Length == 0 || args.Any(a => string.Equals(a, jobName, StringComparison.OrdinalIgnoreCase));
        }

        private static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = true;
            var process = Process.Start(processInfo);
            process.WaitForExit(30_000);
            var exitCode = process.ExitCode;
            Console.WriteLine("Publish ExitCode: " + exitCode.ToString(), "ExecuteCommand");
            process.Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Datadog.Core.Tools
{
    public class PerformanceBenchmark
    {
        public PerformanceBenchmark(string projectName)
        {
            ProjectName = projectName;
        }

        private static readonly string _frameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

#if DEBUG
        public string BuildConfiguration { get; set; } = "Debug";
#else
        public string BuildConfiguration { get; set; } = "Release";
#endif

        public string ProfilerVersion { get; set; } = EnvironmentTools.GetCurrentTracerVersion();

        public string FrameworkDescription { get; set; } = _frameworkDescription;

        public string OSDescription { get; set; } = RuntimeInformation.OSDescription;

        public string OSArchitecture { get; set; } = RuntimeInformation.OSArchitecture.ToString();

        public string ProcessArchitecture { get; set; } = RuntimeInformation.ProcessArchitecture.ToString();

        public bool ProjectName { get; set; }

        public bool ProfilerEnabled { get; set; }

        public string OperationName { get; set; }

        public DateTime TestStart { get; set; }

        public DateTime TestEnd { get; set; }

        public decimal FirstCallMilliseconds { get; set; }

        public double AverageCallMilliseconds { get; set; }

        public double TotalMilliseconds { get; set; }

        public decimal OperationCount { get; set; }

        public Dictionary<string, int> ExceptionCounts { get; set; }

        /// <summary>
        /// Gets or sets the placeholder for commentary on unusual results
        /// </summary>
        public string Comments { get; set; } = string.Empty;

        public void Save()
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();

            var fileFriendlyDate = TestStart.ToString("yyyy-dd-mm_HH-mm-ss");
            var projectPath = Path.Combine(solutionDirectory, "performance", ProjectName);
            var resultsPath = Path.Combine(projectPath, "results");

            if (!Directory.Exists(resultsPath))
            {
                Directory.CreateDirectory(resultsPath);
            }

            var fileName = $"{ProjectName}_{ProfilerVersion}_{BuildConfiguration}_{fileFriendlyDate}";
            if (ProfilerEnabled)
            {
                fileName += "_Profiled";
            }
            else
            {
                fileName += "_NonProfiled";
            }

            var filePath = Path.Combine(resultsPath, $"{fileName}.json");

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public bool IsCoreClr()
        {
            return _frameworkDescription.Contains("core");
        }
    }
}

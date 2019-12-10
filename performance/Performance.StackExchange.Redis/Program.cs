using System;
using System.IO;
using BenchmarkDotNet.Running;
using Datadog.Core.Tools;

namespace Performance.StackExchange.Redis
{
    class Program
    {
        private const string _benchmarkResultFolder = "BenchmarkDotNet.Artifacts";

        public static void Main(string[] args)
        {
            var benchmarkDate = DateTime.Now;

#if DEBUG
            StackExchangeRedisBenchmarks.DoEvalSetOnLargeString();
            var debugModeConcurrencyRunner = new ConcurrencyHelper();
            debugModeConcurrencyRunner.RegisterLevel(
                () =>
                {
                    StackExchangeRedisBenchmarks.DoEvalSetOnLargeString();
                },
                1_000,
                friendlyName: "redis eval load test",
                numberOfRegisters: 20);
            debugModeConcurrencyRunner.Start().Wait();
            var averageRuntime = debugModeConcurrencyRunner.GetAverageActionRuntime();
#else
            var redisSummary = BenchmarkRunner.Run<StackExchangeRedisBenchmarks>();
            Save("Performance.StackExchange.Redis", DateTime.Now);
#endif
        }

        public static void Save(string benchmarkName, DateTime date)
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();

            var fileFriendlyDate = date.ToString("yyyy-dd-mm_HH-mm-ss");
            var benchmarkDirectory = Path.Combine(solutionDirectory, "performance", "benchmarks");

            if (!Directory.Exists(benchmarkDirectory))
            {
                Directory.CreateDirectory(benchmarkDirectory);
            }

            var resultsPath = Path.Combine(benchmarkDirectory, benchmarkName);

            if (!Directory.Exists(resultsPath))
            {
                Directory.CreateDirectory(resultsPath);
            }

            var persistedDirectoryName = $"{benchmarkName}_{fileFriendlyDate}_{EnvironmentTools.GetTracerVersion()}_{EnvironmentTools.GetBuildConfiguration()}";

            if (EnvironmentTools.IsConfiguredToProfile(typeof(Program)))
            {
                persistedDirectoryName += "_Profiled";
            }
            else
            {
                persistedDirectoryName += "_NonProfiled";
            }

            var currentDirectory = Environment.CurrentDirectory;

            var resultDirectory = Path.Combine(currentDirectory, _benchmarkResultFolder);

            if (!Directory.Exists(resultDirectory))
            {
                throw new Exception("Can't find the benchmarks to save");
            }

            var ultimateDirectory = Path.Combine(resultsPath, persistedDirectoryName);

            Directory.Move(resultDirectory, ultimateDirectory);
        }
    }
}

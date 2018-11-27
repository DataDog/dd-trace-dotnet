using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TestHelper
    {
        protected TestHelper(string sampleAppName, ITestOutputHelper output)
        {
            SampleAppName = sampleAppName;
            Output = output;

            Output.WriteLine($"Platform: {GetPlatform()}");
            Output.WriteLine($"Configuration: {BuildParameters.Configuration}");
            Output.WriteLine($"TargetFramework: {BuildParameters.TargetFramework}");
            Output.WriteLine($".NET Core: {BuildParameters.CoreClr}");
            Output.WriteLine($"Application: {GetSampleApplicationPath()}");
            Output.WriteLine($"Profiler DLL: {GetProfilerDllPath()}");
        }

        protected string SampleAppName { get; }

        protected ITestOutputHelper Output { get; }

        public static string GetPlatform()
        {
            return Environment.Is64BitProcess ? "x64" : "x86";
        }

        public static string GetOS()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "win" :
                   Environment.OSVersion.Platform == PlatformID.Unix ? "linux" :
                   Environment.OSVersion.Platform == PlatformID.MacOSX ? "osx" :
                                                                          string.Empty;
        }

        public static string GetRuntimeIdentifier()
        {
            return BuildParameters.CoreClr ? string.Empty : $"{GetOS()}-{GetPlatform()}";
        }

        public static string GetSolutionDirectory()
        {
            string currentDirectory = Environment.CurrentDirectory;

            int index = currentDirectory.Replace('\\', '/')
                                        .LastIndexOf("/test/", StringComparison.InvariantCultureIgnoreCase);

            return currentDirectory.Substring(0, index);
        }

        public static string GetProfilerDllPath()
        {
            return Path.Combine(
                GetSolutionDirectory(),
                "src",
                "Datadog.Trace.ClrProfiler.Native",
                "bin",
                BuildParameters.Configuration,
                GetPlatform(),
                "Datadog.Trace.ClrProfiler.Native." + (GetOS() == "win" ? "dll" : "so"));
        }

        public string GetSampleApplicationPath()
        {
            string appFileName = BuildParameters.CoreClr ? $"Samples.{SampleAppName}.dll" : $"Samples.{SampleAppName}.exe";
            string binDir = Path.Combine(
                GetSolutionDirectory(),
                "samples",
                $"Samples.{SampleAppName}",
                "bin");

            if (GetOS() == "win")
            {
                return Path.Combine(
                                binDir,
                                GetPlatform(),
                                BuildParameters.Configuration,
                                BuildParameters.TargetFramework,
                                GetRuntimeIdentifier(),
                                appFileName);
            }
            else
            {
                return Path.Combine(
                                binDir,
                                BuildParameters.Configuration,
                                BuildParameters.TargetFramework,
                                GetRuntimeIdentifier(),
                                "publish",
                                appFileName);
            }
        }

        public Process StartSample(int traceAgentPort, string arguments = null)
        {
            // get path to native profiler dll
            string profilerDllPath = GetProfilerDllPath();
            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            // get path to sample app that the profiler will attach to
            string sampleAppPath = GetSampleApplicationPath();
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            return ProfilerHelper.StartProcessWithProfiler(
                sampleAppPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath,
                arguments,
                traceAgentPort: traceAgentPort);
        }

        public ProcessResult RunSampleAndWaitForExit(int traceAgentPort, string arguments = null)
        {
            Process process = StartSample(traceAgentPort, arguments);

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            int exitCode = process.ExitCode;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }

        public Process StartIISExpress(int traceAgentPort, int iisPort)
        {
            // get path to native profiler dll
            string profilerDllPath = GetProfilerDllPath();
            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            var sampleDir = Path.Combine(
                GetSolutionDirectory(),
                "samples",
                $"Samples.{SampleAppName}");

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            var exe = $"C:\\Program Files{(Environment.Is64BitProcess ? string.Empty : " (x86)")}\\IIS Express\\iisexpress.exe";
            var args = new string[]
                {
                    $"/clr:v4.0",
                    $"/path:{sampleDir}",
                    $"/systray:false",
                    $"/port:{iisPort}",
                    $"/trace:info",
                };

            Output.WriteLine($"[webserver] starting {exe} {string.Join(" ", args)}");

            var process = ProfilerHelper.StartProcessWithProfiler(
                exe,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath,
                arguments: string.Join(" ", args),
                redirectStandardInput: true,
                traceAgentPort: traceAgentPort);

            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    Output.WriteLine($"[webserver][stdout] {line}");

                    if (line.Contains("IIS Express is running"))
                    {
                        wh.Set();
                    }
                }
            });

            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardError.ReadLine()) != null)
                {
                    Output.WriteLine($"[webserver][stderr] {line}");
                }
            });

            wh.WaitOne(5000);
            return process;
        }

        protected void ValidateSpans<T>(IEnumerable<MockTracerAgent.Span> spans, Func<MockTracerAgent.Span, T> mapper, IEnumerable<T> expected)
        {
            var spanLookup = new Dictionary<T, int>();
            foreach (var span in spans)
            {
                var key = mapper(span);
                if (spanLookup.ContainsKey(key))
                {
                    spanLookup[key]++;
                }
                else
                {
                    spanLookup[key] = 1;
                }
            }

            var missing = new List<T>();
            foreach (var e in expected)
            {
                var found = spanLookup.ContainsKey(e);
                if (found)
                {
                    if (--spanLookup[e] <= 0)
                    {
                        spanLookup.Remove(e);
                    }
                }
                else
                {
                    missing.Add(e);
                }
            }

            foreach (var e in missing)
            {
                Assert.True(false, $"no span found for `{e}`, remaining spans: `{string.Join(", ", spanLookup.Select(kvp => $"{kvp.Key}").ToArray())}`");
            }
        }

        protected async Task AssertHttpSpan(
            string path,
            int agentPort,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            string expectedSpanType,
            string expectedOperationName,
            string expectedResourceName)
        {
            List<MockTracerAgent.Span> spans;

            using (var agent = new MockTracerAgent(agentPort))
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync($"http://localhost:{httpPort}" + path);
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.Equal(expectedHttpStatusCode, response.StatusCode);

                spans = agent.WaitForSpans(1);
                Assert.True(spans.Count == 1, "expected one span");
            }

            MockTracerAgent.Span span = spans[0];
            Assert.Equal(expectedSpanType, span.Type);
            Assert.Equal(expectedOperationName, span.Name);
            Assert.Equal(expectedResourceName, span.Resource);
        }

        internal class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                Add(new Tuple<T1, T2>(item, item2));
            }
        }
    }
}

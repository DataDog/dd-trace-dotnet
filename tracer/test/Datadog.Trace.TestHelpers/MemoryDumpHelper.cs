// <copyright file="MemoryDumpHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers
{
    public class MemoryDumpHelper
    {
        private static string _path;

        private static IProgress<string> _output;

        public static bool IsAvailable => _path != null;

        public static async Task InitializeAsync(IProgress<string> progress)
        {
            _output = progress;

            if (!EnvironmentTools.IsWindows())
            {
                var dotnetRuntimeFolder = Path.GetDirectoryName(typeof(object).Assembly.Location);
                _path = Path.Combine(dotnetRuntimeFolder!, "createdump");
                return;
            }

            // We don't know if procdump is available, so download it fresh
            const string url = "https://download.sysinternals.com/files/Procdump.zip";
            var client = new HttpClient();
            var zipFilePath = Path.GetTempFileName();
            _output?.Report($"Downloading Procdump to '{zipFilePath}'");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                using var bodyStream = await response.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open(zipFilePath, FileMode.Create);
                await bodyStream.CopyToAsync(streamToWriteTo);
            }

            var unpackedDirectory = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetTempFileName()));
            _output?.Report($"Procdump downloaded. Unpacking to '{unpackedDirectory}'");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, unpackedDirectory);

            _path = Path.Combine(unpackedDirectory, "procdump.exe");
        }

        public static void MonitorCrashes(string exe)
        {
#if !NETFRAMEWORK
            return;
#else
            _ = Task.Run(() =>
            {
                var args = $"-ma -accepteula -e -w {Path.GetFileName(exe)} -o {Path.GetTempPath()}";

                using var dumpToolProcess = Process.Start(new ProcessStartInfo(_path, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                })!;

                using var helper = new ProcessHelper(dumpToolProcess);
                dumpToolProcess.WaitForExit();
                helper.Drain();

                if (helper.Process.ExitCode == -2)
                {
                    // No dump was captured but there was no error, the process probably didn't crash
                    return;
                }

                _output?.Report($"[dump][stdout] {helper.StandardOutput}");
                _output?.Report($"[dump][stderr] {helper.ErrorOutput}");
            });
#endif
        }

        public static bool CaptureMemoryDump(Process process)
        {
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                var args = EnvironmentTools.IsWindows() ? $"-ma {process.Id} -accepteula" : process.Id.ToString();
                return CaptureMemoryDump(args);
            }
            catch (Exception ex)
            {
                _output?.Report("Error taking memory dump: " + ex);
                return false;
            }
        }

        private static bool CaptureMemoryDump(string args)
        {
            _output?.Report($"Capturing memory dump using '{_path} {args}'");

            using var dumpToolProcess = Process.Start(new ProcessStartInfo(_path, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            using var helper = new ProcessHelper(dumpToolProcess);
            dumpToolProcess.WaitForExit(30_000);
            helper.Drain();
            _output?.Report($"[dump][stdout] {helper.StandardOutput}");
            _output?.Report($"[dump][stderr] {helper.ErrorOutput}");

            if (dumpToolProcess.ExitCode == 0)
            {
                _output?.Report($"Memory dump successfully captured using '{_path} {args}'.");
            }
            else
            {
                _output?.Report($"Failed to capture memory dump using '{_path} {args}'. Exit code was {dumpToolProcess.ExitCode}.");
            }

            return true;
        }
    }
}

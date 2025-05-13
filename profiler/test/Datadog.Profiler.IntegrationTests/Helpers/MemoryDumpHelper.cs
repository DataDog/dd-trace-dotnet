// <copyright file="MemoryDumpHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    // This is a Windows only implementation
    public class MemoryDumpHelper
    {
        private static string _path;

        public static bool IsAvailable => _path != null;

        public static async Task InitializeAsync(ITestOutputHelper output)
        {
            // We don't know if procdump is available, so download it fresh
            const string url = "https://download.sysinternals.com/files/Procdump.zip";
            var client = new HttpClient();
            var zipFilePath = Path.GetTempFileName();
            output.WriteLine($"Downloading Procdump to '{zipFilePath}'");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                using var bodyStream = await response.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open(zipFilePath, FileMode.Create);
                await bodyStream.CopyToAsync(streamToWriteTo);
            }

            var unpackedDirectory = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetTempFileName()));
            output.WriteLine($"Procdump downloaded. Unpacking to '{unpackedDirectory}'");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, unpackedDirectory);

            var executable = Environment.Is64BitProcess ? "procdump64.exe" : "procdump.exe";

            _path = Path.Combine(unpackedDirectory, executable);
        }

        public static bool CaptureMemoryDump(Process process, string outputFolder, ITestOutputHelper output = null)
        {
            if (!IsAvailable)
            {
                output.WriteLine("Procdump is not available...");
                return false;
            }

            try
            {
                var args = $"-ma -accepteula {process.Id} {outputFolder}";
                return CaptureMemoryDump(args, output);
            }
            catch (Exception ex)
            {
                output.WriteLine("Error taking memory dump: " + ex);
                return false;
            }
        }

        private static bool CaptureMemoryDump(string args, ITestOutputHelper output)
        {
            output.WriteLine($"Capturing memory dump using '{_path} {args}'");

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
            output.WriteLine($"[dump][stdout] {helper.StandardOutput}");
            output.WriteLine($"[dump][stderr] {helper.ErrorOutput}");

            if (dumpToolProcess.ExitCode == 0)
            {
                output.WriteLine($"Memory dump successfully captured using '{_path} {args}'.");
            }
            else
            {
                output.WriteLine($"Failed to capture memory dump using '{_path} {args}'. Exit code was {dumpToolProcess.ExitCode}.");
            }

            return true;
        }
    }
}

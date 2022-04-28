// <copyright file="ProcessInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Datadog.Trace.Configuration;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessInfo
    {
        public ProcessInfo(Process process, string? baseDirectory = null, IConfigurationSource? appSettings = null)
        {
            Name = process.ProcessName;
            Id = process.Id;
            EnvironmentVariables = ProcessEnvironment.ReadVariables(process);
            MainModule = process.MainModule?.FileName;

            Modules = ProcessEnvironment.ReadModules(process);

            DotnetRuntime = DetectRuntime(Modules);
            Architecture = GetProcessArchitecture(process);
            Configuration = ExtractConfigurationSource(baseDirectory, appSettings);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessInfo"/> class to be used for unit tests.
        /// </summary>
        internal ProcessInfo(string name, int id, IReadOnlyDictionary<string, string> environmentVariables, string mainModule, string[] modules)
        {
            Name = name;
            Id = id;
            EnvironmentVariables = environmentVariables;
            MainModule = mainModule;
            Modules = modules;

            DotnetRuntime = DetectRuntime(Modules);
            Configuration = ExtractConfigurationSource(null, null);
        }

        [Flags]
        public enum Runtime
        {
            Unknown = 0,
            NetFx = 1,
            NetCore = 2,
            Mixed = NetFx | NetCore
        }

        public string Name { get; }

        public int Id { get; }

        public string[] Modules { get; }

        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

        public string? MainModule { get; }

        public Runtime DotnetRuntime { get; }

        public Architecture? Architecture { get; }

        public IConfigurationSource? Configuration { get; }

        public static ProcessInfo? GetProcessInfo(int pid, string? baseDirectory = null, IConfigurationSource? appSettings = null)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessInfo(process, baseDirectory, appSettings);
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to fetch process information: " + ex.Message);
                return null;
            }
        }

        public IReadOnlyList<int> GetChildProcesses()
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return Array.Empty<int>();
            }

            var query = $"Select * From Win32_Process Where ParentProcessId = {Id}";
            using var searcher = new ManagementObjectSearcher(query);
            using var processList = searcher.Get();

            var result = new List<int>();

            foreach (var obj in processList)
            {
                result.Add(Convert.ToInt32(obj.GetPropertyValue("ProcessId")));
                obj.Dispose();
            }

            return result;
        }

        private static Runtime DetectRuntime(string[] modules)
        {
            var result = Runtime.Unknown;

            foreach (var module in modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    result |= Runtime.NetFx;
                }
                else if (fileName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("libcoreclr.so", StringComparison.OrdinalIgnoreCase))
                {
                    result |= Runtime.NetCore;
                }
            }

            return result;
        }

        private static IConfigurationSource? LoadApplicationConfig(string? mainModule)
        {
            if (mainModule == null)
            {
                return null;
            }

            var folder = Path.GetDirectoryName(mainModule);

            if (folder == null)
            {
                return null;
            }

            var configFileName = Path.GetFileName(mainModule) + ".config";
            var configPath = Path.Combine(folder, configFileName);

            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                var document = XDocument.Load(configPath);

                var appSettings = document.Element("configuration")?.Element("appSettings");

                if (appSettings == null)
                {
                    return null;
                }

                var settings = new Dictionary<string, string>();

                foreach (var setting in appSettings.Elements())
                {
                    var key = setting.Attribute("key")?.Value;
                    var value = setting.Attribute("value")?.Value;

                    if (key != null)
                    {
                        settings[key] = value ?? string.Empty;
                    }
                }

                return new DictionaryConfigurationSource(settings);
            }
            catch (Exception ex)
            {
                Utils.WriteWarning($"An error occured while parsing the configuration file {configPath}: {ex.Message}");
                return null;
            }
        }

        private static Architecture? GetProcessArchitecture(Process process)
        {
            try
            {
                // WOW64 is only available on 64-bit versions of Windows (x64 or ARM64)
                if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) && Environment.Is64BitOperatingSystem)
                {
                    // https://docs.microsoft.com/en-us/windows/win32/api/wow64apiset/nf-wow64apiset-iswow64process
                    if (!Windows.NativeMethods.IsWow64Process(process.Handle, out var isWow64))
                    {
                        // p/invoke failed
                        Utils.WriteWarning(CannotDetermineProcessArchitecture());
                        return null;
                    }

                    if (isWow64)
                    {
                        // We can't tell if process is x86 or ARM32 without IsWow64Process2(),
                        // but we don't support ARM64 on Windows yet, so assume x86.
                        const Architecture x86 = System.Runtime.InteropServices.Architecture.X86;
                        Utils.Write(DetectedProcessArchitecture(x86));
                        return x86;
                    }
                }

                // otherwise, assume process arch matches OS arch
                var processArchitecture = RuntimeInformation.OSArchitecture;
                Utils.Write(DetectedProcessArchitecture(processArchitecture));
                return processArchitecture;
            }
            catch (Exception ex)
            {
                Utils.WriteWarning(CannotDetermineProcessArchitecture(), ex);
                return null;
            }
        }

        private IConfigurationSource ExtractConfigurationSource(string? baseDirectory, IConfigurationSource? appSettings)
        {
            baseDirectory ??= Path.GetDirectoryName(MainModule);

            var configurationSource = new CompositeConfigurationSource();

            configurationSource.Add(new DictionaryConfigurationSource(EnvironmentVariables));

            if (appSettings != null)
            {
                configurationSource.Add(appSettings);
            }
            else if (DotnetRuntime.HasFlag(Runtime.NetFx))
            {
                var appConfigSource = LoadApplicationConfig(MainModule);

                if (appConfigSource != null)
                {
                    configurationSource.Add(appConfigSource);
                }
            }

            if (GlobalSettings.TryLoadJsonConfigurationFile(configurationSource, baseDirectory, out var jsonConfigurationSource))
            {
                configurationSource.Add(jsonConfigurationSource);
            }

            return configurationSource;
        }
    }
}

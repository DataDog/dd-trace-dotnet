// <copyright file="ProcessInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessInfo
    {
        public ProcessInfo(Process process)
        {
            Name = process.ProcessName;
            Id = process.Id;
            EnvironmentVariables = ProcessEnvironment.ReadVariables(process);
            MainModule = process.MainModule?.FileName;

            Modules = process.Modules
                .OfType<ProcessModule>()
                .Select(p => p.FileName)
                .Where(p => p != null)
                .ToArray()!;

            DotnetRuntime = DetectRuntime(Modules);
            Configuration = ExtractConfigurationSource();
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
            Configuration = ExtractConfigurationSource();
        }

        public enum Runtime
        {
            Unknown,
            NetFx,
            NetCore
        }

        public string Name { get; }

        public int Id { get; }

        public string[] Modules { get; }

        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

        public string? MainModule { get; }

        public Runtime DotnetRuntime { get; }

        public IConfigurationSource? Configuration { get; }

        public static ProcessInfo? GetProcessInfo(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessInfo(process);
            }
            catch (Exception ex)
            {
                Utils.WriteError("Error while trying to fetch process information: " + ex.Message);
                return null;
            }
        }

        private static Runtime DetectRuntime(string[] modules)
        {
            foreach (var module in modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return Runtime.NetFx;
                }

                if (fileName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("libcoreclr.so", StringComparison.OrdinalIgnoreCase))
                {
                    return Runtime.NetCore;
                }
            }

            return Runtime.Unknown;
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

        private IConfigurationSource ExtractConfigurationSource()
        {
            var configurationSource = new CompositeConfigurationSource();

            configurationSource.Add(new DictionaryConfigurationSource(EnvironmentVariables));

            if (DotnetRuntime == Runtime.NetFx)
            {
                var appConfigSource = LoadApplicationConfig(MainModule);

                if (appConfigSource != null)
                {
                    configurationSource.Add(appConfigSource);
                }
            }

            if (GlobalSettings.TryLoadJsonConfigurationFile(configurationSource, Path.GetDirectoryName(MainModule), out var jsonConfigurationSource))
            {
                configurationSource.Add(jsonConfigurationSource);
            }

            return configurationSource;
        }
    }
}

// <copyright file="ProfilerEngineVersionInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Reflection;

namespace Datadog.Profiler
{
    internal class ProfilerEngineVersionInfo
    {
        private ProfilerEngineVersionInfo(string buildConfigurationMoniker, string fileVersion, string informationalVersion, string assemblyName)
        {
            BuildConfigurationMoniker = buildConfigurationMoniker;
            FileVersion = fileVersion;
            InformationalVersion = informationalVersion;
            AssemblyName = assemblyName;
        }

        public string BuildConfigurationMoniker { get; }

        public string FileVersion { get; }

        public string InformationalVersion { get; }

        public string AssemblyName { get; }

        internal static ProfilerEngineVersionInfo CreateNewInstance()
        {
            Assembly profilerEngineAssembly;

            try
            {
                profilerEngineAssembly = typeof(ProfilerEngine).Assembly;
            }
            catch
            {
                profilerEngineAssembly = null;
            }

#if DEBUG
            string buildConfigurationMoniker = "Debug";
#else
            string buildConfigurationMoniker = "Release";
#endif

            return new ProfilerEngineVersionInfo(
                            buildConfigurationMoniker,
                            profilerEngineAssembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
                            profilerEngineAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                            profilerEngineAssembly?.FullName);
        }
    }
}

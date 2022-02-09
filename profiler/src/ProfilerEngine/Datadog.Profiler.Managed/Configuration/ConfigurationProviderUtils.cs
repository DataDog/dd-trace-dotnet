// <copyright file="ConfigurationProviderUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Profiler;
using Datadog.Util;

namespace Datadog.Configuration
{
    public static class ConfigurationProviderUtils
    {
        public const string ProductFamily = "DotNet";

        private static bool? _isWindows = null;
        private static string _logDirectory = null;
        private static string _pprofDirectory = null;

        public static string GetMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return "Unknown-Host";
            }
        }

        /// <summary>
        /// Figure out what the DDSevice tag should be, if the user specified nothing.
        /// Try to get the same value as the Tracer does, so that the user gets the same value for the same app.
        /// However, under some circumstances, the Tracer uses tracer specific data.
        /// In such cases, we cannot have a completely identical name like for example:
        ///   - In some application servers, the Tracer considers whether the app makes GET or POST calls to downstream dependencies;
        ///   - Users may override the service tag programmatically on a per span basis.
        ///   - ...
        /// Note that if the DD_SERVICE environment variable is set, then the value returned by this method will probably
        /// be overwritten anyway (<see cref="EnvironmentVariablesConfigurationProvider" />).
        /// </summary>
        public static string GetDdServiceFallback()
        {
            try
            {
                try
                {
                    if (TryGetClassicAspNetSiteName(out var aspNetSiteName))
                    {
                        return aspNetSiteName;
                    }
                }
                catch
                {
                    // Just fall back to the next option.
                    // Also, note the doc comments on 'TryGetClassicAspNetSiteName(..)' about partial trust.
                }

                return Assembly.GetEntryAssembly()?.GetName().Name.Trim() ?? CurrentProcess.GetName()?.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static string GetOsSpecificDefaultLogDirectory()
        {
            string osSpecificDefaultLogDirectory = _logDirectory;

            if (osSpecificDefaultLogDirectory == null)
            {
                try
                {
                    if (IsWindowsFileSystem())
                    {
                        string commonAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        osSpecificDefaultLogDirectory =
                            Path.Combine(
                                commonAppDataDir,
                                DefaultDirectories.WindowsAppDataRoot,
                                DefaultDirectories.WindowsLogsDir,
                                ProductFamily);
                    }
                    else
                    {
                        osSpecificDefaultLogDirectory =
                            Path.Combine(
                                DefaultDirectories.LinuxAppDataRoot,
                                DefaultDirectories.LinuxLogsDir,
                                ProductFamily.ToLower());
                    }

                    _logDirectory = osSpecificDefaultLogDirectory;
                }
                catch
                {
                    // If something went bad during determination, use null. That will force the logger to use it's built-in default mechanism.
                    osSpecificDefaultLogDirectory = null;
                }
            }

            return osSpecificDefaultLogDirectory;
        }

        public static string GetOsSpecificDefaultPProfDirectory()
        {
            string osSpecificDefaultPProfDirectory = _pprofDirectory;

            if (osSpecificDefaultPProfDirectory == null)
            {
                try
                {
                    if (IsWindowsFileSystem())
                    {
                        string commonAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        osSpecificDefaultPProfDirectory =
                            Path.Combine(
                                commonAppDataDir,
                                DefaultDirectories.WindowsAppDataRoot,
                                DefaultDirectories.WindowsProfilesDir,
                                ProductFamily);
                    }
                    else
                    {
                        osSpecificDefaultPProfDirectory =
                            Path.Combine(
                                DefaultDirectories.LinuxAppDataRoot,
                                DefaultDirectories.LinuxProfilesDir,
                                ProductFamily.ToLower());
                    }

                    _pprofDirectory = osSpecificDefaultPProfDirectory;
                }
                catch
                {
                    // If something went bad during determination, use null. That will force the logger to use it's built-in default mechanism.
                    osSpecificDefaultPProfDirectory = null;
                }
            }

            return osSpecificDefaultPProfDirectory;
        }

        public static bool TryParseBooleanSettingStr(string booleanSettingStr, bool booleanSettingDefaultVal, out bool booleanSettingVal)
        {
            if (booleanSettingStr != null)
            {
                booleanSettingStr = booleanSettingStr.Trim();

                if (booleanSettingStr.Equals("false", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("no", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("n", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("f", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    booleanSettingVal = false;
                    return true;
                }

                if (booleanSettingStr.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("yes", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("y", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("t", StringComparison.OrdinalIgnoreCase)
                        || booleanSettingStr.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    booleanSettingVal = true;
                    return true;
                }
            }

            booleanSettingVal = booleanSettingDefaultVal;
            return false;
        }

        /// <summary>
        /// ! This method should be called from within a try-catch block !
        /// If the application is running in partial trust, then trying to call this method will result in
        /// a SecurityException to be thrown at the method CALLSITE, not inside the <c>TryGetClassicAspNetSiteName(..)</c> method itself,
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetClassicAspNetSiteName(out string siteName)
        {
#if NETFRAMEWORK
            // On Net Fx only (System.Web.dll is not available on Net Core), check whether the app is hosted in ASP.NET.
            // If yes, return "SiteName/ApplicationVirtualPath" (note: ApplicationVirtualPath includes a leading slash).
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                siteName = (System.Web.Hosting.HostingEnvironment.SiteName ?? string.Empty)
                         + (System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath ?? string.Empty);

                siteName = siteName.Trim().TrimEnd('/');

                return (siteName.Length > 0);
            }
#endif
            siteName = null;
            return false;
        }

        private static bool IsWindowsFileSystem()
        {
            bool? isWindowsFileSystem = _isWindows;

            if (!isWindowsFileSystem.HasValue)
            {
                PlatformID platformID = Environment.OSVersion.Platform;
                switch (platformID)
                {
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.Win32NT:
                    case PlatformID.WinCE:
                        isWindowsFileSystem = true;
                        break;

                    case PlatformID.Unix:
                    case PlatformID.MacOSX:
                        isWindowsFileSystem = false;
                        break;

                    case PlatformID.Xbox:
                    default:
                        string errMsg = $"Unexpected OS PlatformID: \"{platformID}\" ({((int)platformID)})";
                        Log.Error(Log.WithCallInfo(nameof(ConfigurationProviderUtils)), errMsg);
                        throw new InvalidOperationException(errMsg);
                }

                _isWindows = isWindowsFileSystem;
            }

            return isWindowsFileSystem.Value;
        }

        private static class DefaultDirectories
        {
            public const string WindowsAppDataRoot = @"Datadog-APM\";   // relative to Environment.SpecialFolder.CommonApplicationData
            public const string LinuxAppDataRoot = @"/var/log/datadog/";  // global path

            public const string WindowsLogsDir = @"logs";               // relative to AppDataRoot
            public const string LinuxLogsDir = @"";                       // relative to AppDataRoot

            public const string WindowsProfilesDir = @"PProf-Files";    // relative to AppDataRoot
            public const string LinuxProfilesDir = @"pprof-files";        // relative to AppDataRoot
        }
    }
}
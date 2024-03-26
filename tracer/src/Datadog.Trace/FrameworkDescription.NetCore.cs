// <copyright file="FrameworkDescription.NetCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Datadog.Trace
{
    internal partial class FrameworkDescription
    {
        private static FrameworkDescription _instance = null;

        public static FrameworkDescription Instance
        {
            get { return _instance ?? (_instance = Create()); }
        }

        public static FrameworkDescription Create()
        {
            var frameworkName = "unknown";
            var frameworkVersion = "unknown";
            var osPlatform = "unknown";
            var osArchitecture = "unknown";
            var processArchitecture = "unknown";
            var osDescription = "unknown";

            try
            {
                try
                {
                    // RuntimeInformation.FrameworkDescription returns a string like ".NET Framework 4.7.2" or ".NET Core 2.1",
                    // we want to return everything before the last space
                    frameworkVersion = RuntimeInformation.FrameworkDescription;
                    int index = frameworkVersion.LastIndexOf(' ');
                    frameworkName = frameworkVersion.Substring(0, index).Trim();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting framework name from RuntimeInformation");
                }

                if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    osPlatform = Trace.OSPlatformName.Windows;
                }
                else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    osPlatform = Trace.OSPlatformName.Linux;
                }
                else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    osPlatform = Trace.OSPlatformName.MacOS;
                }

                osArchitecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
                frameworkVersion = GetNetCoreOrNetFrameworkVersion();
#if NET8_0_OR_GREATER
                osDescription = RuntimeInformation.OSDescription;
#else
                osDescription = GetOsDescription();
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting framework description.");
            }

            return new FrameworkDescription(
                name: frameworkName,
                productVersion: frameworkVersion,
                osPlatform: osPlatform,
                osArchitecture: osArchitecture,
                processArchitecture: processArchitecture,
                osDescription: osDescription);
        }

        public bool IsCoreClr()
        {
            return Name.ToLowerInvariant().Contains("core") || IsNet5();
        }

        private static string GetNetCoreOrNetFrameworkVersion()
        {
            string productVersion = null;

            if (Environment.Version.Major == 3 || Environment.Version.Major >= 5)
            {
                // Environment.Version returns "4.x" in .NET Core 2.x,
                // but it is correct since .NET Core 3.0.0
                productVersion = Environment.Version.ToString();
            }

            if (productVersion == null)
            {
                try
                {
                    // try to get product version from assembly path
#if NET5_0_OR_GREATER
                    // Can't use RootAssembly.CodeBase in .NET 5+
                    var location = RootAssembly.Location;
#else
                    var location = RootAssembly.CodeBase;
#endif
                    Match match = Regex.Match(
                        location,
                        @"[\\/][^\\/]*microsoft\.netcore\.app[\\/](\d+\.\d+\.\d+[^/]*)[\\/]",
                        RegexOptions.IgnoreCase);

                    if (match.Success && match.Groups.Count > 0 && match.Groups[1].Success)
                    {
                        productVersion = match.Groups[1].Value;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting .NET Core version from assembly path");
                }
            }

            if (productVersion == null)
            {
                // if we fail to extract version from assembly path,
                // fall back to the [AssemblyInformationalVersion] or [AssemblyFileVersion]
                productVersion = GetVersionFromAssemblyAttributes();
            }

            if (productVersion == null)
            {
                // at this point, everything else has failed (this is probably the same as [AssemblyFileVersion] above)
                productVersion = Environment.Version.ToString();
            }

            return productVersion;
        }

        private static string GetOsDescription()
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return RuntimeInformation.OSDescription;
            }

            // back-ports the .NET 8 implementation of RuntimeInformation.OSDescription
            // for Linux. This gives us a "friendly" distribution name
            // https://github.com/dotnet/runtime/blob/9fe22288c3b78dc681dbb02401c4197609cdb544/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Unix.cs#L34C32-L34C54
            const string filename = "/etc/os-release";

            if (File.Exists(filename))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(filename);
                }
                catch
                {
                    return null;
                }

#if NETCOREAPP
                // Parse the NAME, PRETTY_NAME, and VERSION fields.
                // These fields are suitable for presentation to the user.
                ReadOnlySpan<char> prettyName = default, name = default, version = default;
                foreach (string line in lines)
                {
                    ReadOnlySpan<char> lineSpan = line.AsSpan();
                    _ = TryGetFieldValue(lineSpan, "PRETTY_NAME=", ref prettyName) ||
                        TryGetFieldValue(lineSpan, "NAME=", ref name) ||
                        TryGetFieldValue(lineSpan, "VERSION=", ref version);

                    // Prefer "PRETTY_NAME".
                    if (!prettyName.IsEmpty)
                    {
                        return new string(prettyName);
                    }
                }

                // Fall back to "NAME[ VERSION]".
                if (!name.IsEmpty)
                {
                    if (!version.IsEmpty)
                    {
                        return string.Concat(name, " ", version);
                    }

                    return new string(name);
                }
#else
                // Parse the NAME, PRETTY_NAME, and VERSION fields.
                // These fields are suitable for presentation to the user.
                string prettyName = default, name = default, version = default;
                foreach (string line in lines)
                {
                    _ = TryGetFieldValue(line, "PRETTY_NAME=", ref prettyName) ||
                        TryGetFieldValue(line, "NAME=", ref name) ||
                        TryGetFieldValue(line, "VERSION=", ref version);

                    // Prefer "PRETTY_NAME".
                    if (!string.IsNullOrEmpty(prettyName))
                    {
                        return prettyName;
                    }
                }

                // Fall back to "NAME[ VERSION]".
                if (!string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(version))
                    {
                        return string.Concat(name, " ", version);
                    }

                    return name;
                }
#endif
            }

            // Fallback to the "default" value
            return RuntimeInformation.OSDescription;

#if NETCOREAPP
            static bool TryGetFieldValue(ReadOnlySpan<char> line, ReadOnlySpan<char> prefix, ref ReadOnlySpan<char> value)
            {
                if (!line.StartsWith(prefix))
                {
                    return false;
                }

                ReadOnlySpan<char> fieldValue = line.Slice(prefix.Length);

                // Remove enclosing quotes.
                if (fieldValue.Length >= 2 &&
                    fieldValue[0] is '"' or '\'' &&
                    fieldValue[0] == fieldValue[^1])
                {
                    fieldValue = fieldValue[1..^1];
                }

                value = fieldValue;
                return true;
            }
#else
            static bool TryGetFieldValue(string line, string prefix, ref string value)
            {
                if (!line.StartsWith(prefix))
                {
                    return false;
                }

                var fieldValue = line.Substring(prefix.Length);

                // Remove enclosing quotes.
                if (fieldValue.Length >= 2 &&
                    fieldValue[0] is '"' or '\'' &&
                    fieldValue[0] == fieldValue[fieldValue.Length - 1])
                {
                    fieldValue = fieldValue.Substring(1, fieldValue.Length - 2);
                }

                value = fieldValue;
                return true;
            }
#endif
        }
    }
}
#endif

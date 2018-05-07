#pragma warning disable SA1214 // Readonly fields must appear before non-readonly fields

using System;
using Datadog.Trace.Logging;

#if !(NETSTANDARD2_0 || NET471)
using System.Linq;
using Microsoft.Win32;
#endif

namespace Datadog.Trace
{
    /// <summary>
    /// Provides helper methods that retrieve information about the current runtime environment.
    /// </summary>
    public static class RuntimeInformation
    {
        private static ILog _log = LogProvider.For<Span>();

#if NETSTANDARD2_0 || NET471
        /// <summary>
        /// Gets the framework version of the current runtime environment.
        /// </summary>
        /// <returns>The framework version of the current runtime environment (e.g. "4.5", "4.5.1", "4.6").</returns>
        public static string GetFrameworkVersion()
        {
            return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        }
#else
        private static readonly KeyVersionPair[] Versions =
        {
            // Values for 4.7.1 and above would normally never be used,
            // since projects targeting that should use a different binary assembly,
            // but they could be used if an assembly built for .NET Framework 4.5-4.7 is used in a runtime 4.7.1+.
            // 461809 is a fake value used as a catch-all for anything higher than 4.7.2 until new versions are released.
            new KeyVersionPair(461809, "> 4.7.2"),
            new KeyVersionPair(461808, "4.7.2"),
            new KeyVersionPair(461308, "4.7.1"),

            new KeyVersionPair(460798, "4.7"),
            new KeyVersionPair(394802, "4.6.2"),
            new KeyVersionPair(394254, "4.6.1"),
            new KeyVersionPair(393295, "4.6"),
            new KeyVersionPair(379893, "4.5.2"),
            new KeyVersionPair(378675, "4.5.1"),
            new KeyVersionPair(378389, "4.5")
        };

        /// <summary>
        /// Gets the framework version of the current runtime environment.
        /// </summary>
        /// <returns>The framework version of the current runtime environment (e.g. "4.5", "4.5.1", "4.6").</returns>
        public static string GetFrameworkVersion()
        {
            try
            {
                // on the full .NET Framework 4.5-4.7, inclusive, query the registry to determine the version of the CLR
                // https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey ndpKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    object value = ndpKey?.GetValue("Release");

                    if (value != null)
                    {
                        string version = Versions.FirstOrDefault(v => (int)value >= v.Key)?.Version;

                        if (version != null)
                        {
                            return version;
                        }
                    }

                    return "Unknown";
                }
            }
            catch (Exception ex)
            {
                _log.ErrorException("Error ocurred trying to determine .NET Framework runtime version.", ex);
                return "Unknown";
            }
        }

        private class KeyVersionPair
        {
            public KeyVersionPair(int key, string version)
            {
                Key = key;
                Version = version;
            }

            public int Key { get; }

            public string Version { get; }
        }
#endif
    }
}

#pragma warning restore SA1214 // Readonly fields must appear before non-readonly fields

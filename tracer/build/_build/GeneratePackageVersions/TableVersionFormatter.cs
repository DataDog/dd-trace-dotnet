using System;
using System.Text.RegularExpressions;

namespace GeneratePackageVersions
{
    internal static class TableVersionFormatter
    {
        public static string FormatVersion(string version)
        {
            if (string.IsNullOrEmpty(version) || version == "-")
            {
                return "-";
            }

            // Convert 65535 to * in version components
            var regex = new Regex(@"(\d+)\.65535\.65535$");
            var match = regex.Match(version);
            if (match.Success)
            {
                return $"{match.Groups[1].Value}.*.*";
            }

            regex = new Regex(@"(\d+\.\d+)\.65535$");
            match = regex.Match(version);
            if (match.Success)
            {
                return $"{match.Groups[1].Value}.*";
            }

            // Handle existing wildcard formats like "4.*.*"
            if (version.Contains("*"))
            {
                return version;
            }

            // Handle explicit version numbers
            return version;
        }

        public static string GetVersionRange(string minVersion, string maxVersion)
        {
            var min = FormatVersion(minVersion);
            var max = FormatVersion(maxVersion);

            if (min == "-" && max == "-")
            {
                return "-";
            }

            return $"{min} → {max}";
        }
    }
}

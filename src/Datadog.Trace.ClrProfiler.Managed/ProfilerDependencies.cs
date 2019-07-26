using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler
{
    internal class ProfilerDependencies
    {
        public OSPlatform[] SupportedPlatforms { get; set; }

        public IntegrationVersionRange FrameworkVersionRange { get; set; }

        public IntegrationVersionRange CoreVersionRange { get; set; }

        public IntegrationVersionRange StandardVersionRange { get; set; }

        public byte[][] Assemblies { get; set; }

        public bool IsMatch(OSPlatform platform, RuntimeType runtimeType, int major, int minor)
        {
            if (!SupportedPlatforms.Contains(platform))
            {
                return false;
            }

            bool isMatch = false;

            switch (runtimeType)
            {
                case RuntimeType.Core:
                    isMatch = IsValidForRange(CoreVersionRange, major, minor);
                    break;
                case RuntimeType.Standard:
                    isMatch = IsValidForRange(StandardVersionRange, major, minor);
                    break;
                case RuntimeType.Framework:
                    isMatch = IsValidForRange(FrameworkVersionRange, major, minor);
                    break;
                default:
                    return false;
            }

            return isMatch;
        }

        private bool IsValidForRange(IntegrationVersionRange range, int major, int minor)
        {
            if (major > range.MaximumMajor)
            {
                return false;
            }

            if (major == range.MaximumMajor && minor > range.MaximumMinor)
            {
                return false;
            }

            if (major < range.MinimumMinor)
            {
                return false;
            }

            if (major == range.MinimumMinor && minor < range.MinimumMinor)
            {
                return false;
            }

            return true;
        }
    }
}

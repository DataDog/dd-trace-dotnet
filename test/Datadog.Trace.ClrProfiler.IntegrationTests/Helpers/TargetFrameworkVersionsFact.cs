using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class TargetFrameworkVersionsFact : FactAttribute
    {
        private const string DotNetFramework = ".NETFramework";
        private const string CoreFramework = ".NETCoreApp";

        public TargetFrameworkVersionsFact(string targetFrameworkVersions)
        {
            var targetFramework = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
            var parts = targetFramework.FrameworkName.Split(',');
            var runtime = parts[0];
            var isCoreClr = runtime.Equals(CoreFramework);

            var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');
            var major = int.Parse(versionParts[0]);
            var minor = int.Parse(versionParts[1]);
            string patch = null;

            if (versionParts.Length == 3)
            {
                patch = versionParts[2];
            }

            string compiledTargetFrameworkString;
            if (isCoreClr)
            {
                compiledTargetFrameworkString = $"netcoreapp{major}.{minor}";
            }
            else
            {
                compiledTargetFrameworkString = $"net{major}{minor}{patch ?? string.Empty}";
            }

            if (targetFrameworkVersions.Split(';').All(s => !s.Equals(compiledTargetFrameworkString, StringComparison.OrdinalIgnoreCase)))
            {
                Skip = $"xUnit target framework does not match {targetFrameworkVersions}";
            }
        }
    }
}

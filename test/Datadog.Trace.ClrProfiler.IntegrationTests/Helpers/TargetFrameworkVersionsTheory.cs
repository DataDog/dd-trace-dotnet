using System;
using System.Linq;
using System.Reflection;
using Datadog.Core.Tools.Extensions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class TargetFrameworkVersionsTheory : TheoryAttribute
    {
        public TargetFrameworkVersionsTheory(string targetFrameworkMonikers)
        {
            var compiledTargetFrameworkString = Assembly.GetExecutingAssembly().GetTargetFrameworkMoniker();

            if (targetFrameworkMonikers.Split(';').All(s => !s.Equals(compiledTargetFrameworkString, StringComparison.OrdinalIgnoreCase)))
            {
                Skip = $"xUnit target framework does not match {targetFrameworkMonikers}";
            }
        }
    }
}

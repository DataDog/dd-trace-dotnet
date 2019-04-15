using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.TestFramework
{
    public sealed class FactExcludingFramework : FactAttribute
    {
        public FactExcludingFramework(string framework)
        {
            var currentFramework = BuildParameters.TargetFramework;

            if (currentFramework.Equals(framework))
            {
                Skip = $"Test ignored for {currentFramework}.";
            }
        }
    }
}

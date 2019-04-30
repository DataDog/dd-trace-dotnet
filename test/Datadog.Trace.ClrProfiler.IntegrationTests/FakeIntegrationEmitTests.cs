using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class FakeIntegrationEmitTests
    {
        private ITestOutputHelper _output;

        public FakeIntegrationEmitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SubmitsTraces()
        {
        }
    }
}

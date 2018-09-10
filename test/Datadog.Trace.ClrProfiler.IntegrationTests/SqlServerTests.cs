using Xunit;

// EFCore targets netstandard2.0, so it requires net461 or higher or netcoreapp2.0 or higher
#if !NET452

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SqlServerTests : TestHelper
    {
        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent())
            {
                using (var process = StartSample("SqlServer"))
                {
                    process.WaitForExit();
                }

                var spans = agent.GetSpans();
                Assert.True(spans.Count > 1);
                foreach (var span in spans)
                {
                    Assert.Equal("sqlserver.query", span.Name);
                    Assert.Equal("Samples.SqlServer", span.Service);
                    Assert.Equal("sql", span.Type);
                }
            }
        }
    }
}

#endif

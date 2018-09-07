using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Xunit;

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

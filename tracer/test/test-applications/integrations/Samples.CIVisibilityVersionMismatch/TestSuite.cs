using Datadog.Trace;
using Xunit;

namespace Samples.CIVisibilityVersionMismatch
{
    public class TestSuite
    {
        [Fact]
        public void CustomSpanTest()
        {
            using var scope = Tracer.Instance.StartActive("custom-operation");
            scope.Span.SetTag("My Custom Tag", "My Custom Value");
        }
    }
}

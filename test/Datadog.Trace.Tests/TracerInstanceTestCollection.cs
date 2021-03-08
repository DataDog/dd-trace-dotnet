using Xunit;

namespace Datadog.Trace.Tests
{
    [CollectionDefinition(nameof(TracerInstanceTestCollection), DisableParallelization = true)]
    public class TracerInstanceTestCollection
    {
    }
}

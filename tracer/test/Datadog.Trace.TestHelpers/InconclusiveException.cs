using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    public class InconclusiveException : XunitException
    {
        public InconclusiveException(string reason)
            : base(reason)
        {
        }
    }
}

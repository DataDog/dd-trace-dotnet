using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public class TestRunners
    {
        public static readonly IEnumerable<string> ValidNames = new[]
                                                                {
                                                                    "testhost",
                                                                    "vstest.console",
                                                                    "xunit.console.x86",
                                                                    "xunit.console.x64"
                                                                };
    }
}

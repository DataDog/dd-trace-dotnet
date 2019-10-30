using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public class TestRunners
    {
        public static readonly IEnumerable<string> ValidNames = new[]
                                                                {
                                                                    "testhost",
                                                                    "testhost.x86",
                                                                    "vstest.console",
                                                                    "xunit.console.x86",
                                                                    "xunit.console.x64",
                                                                    "ReSharperTestRunner64",
                                                                    "ReSharperTestRunner64c"
                                                                };
    }
}

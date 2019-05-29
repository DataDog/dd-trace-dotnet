using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class PackageVersions
    {
        public static IEnumerable<object[]> MongoDB =>

            new List<object[]>
            {
#if VS_COMPILE
                new object[] { string.Empty },
#else
                new object[] { "2.8.1" },
                new object[] { "2.8.0" },
                new object[] { "2.7.3" },
                new object[] { "2.7.2" },
                new object[] { "2.7.1" },
                new object[] { "2.7.0" },
                new object[] { "2.6.1" },
                new object[] { "2.6.0" },
                new object[] { "2.5.1" },
                new object[] { "2.5.0" },
                new object[] { "2.4.4" },
                new object[] { "2.4.3" },
                new object[] { "2.4.2" },
                new object[] { "2.4.1" },
                new object[] { "2.4.0" },
                new object[] { "2.3.0" },
                new object[] { "2.2.4" },
                new object[] { "2.2.3" },
                new object[] { "2.2.2" },
                new object[] { "2.2.1" },
                new object[] { "2.2.0" },
                new object[] { "2.1.1" },
                new object[] { "2.1.0" },
                new object[] { "2.0.2" },
                new object[] { "2.0.1" },
                new object[] { "2.0.0" },
#endif
            };
    }
}
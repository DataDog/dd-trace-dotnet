#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MsmqTests : TestHelper
    {
        private const string ExpectedServiceName = "Samples.Msmq-msmq";

        public MsmqTests(ITestOutputHelper output)
            : base("Msmq", output)
        {
            SetServiceVersion("1.0.0");
        }
    }
}
#endif

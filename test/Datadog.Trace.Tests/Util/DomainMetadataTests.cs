using System;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class DomainMetadataTests
    {
#if !NETCOREAPP

        private const string TestDataKey = "ShouldAvoidAppInsightsAppDomain";

        [Fact]
        public void ShouldAvoidAppInsightsAppDomain()
        {
            static void AppDomainCallback()
            {
                var result = DomainMetadata.ShouldAvoidAppDomain();
                AppDomain.CurrentDomain.SetData(TestDataKey, result);
            }

            var domain1 = AppDomain.CreateDomain("ApplicationInsights", null, AppDomain.CurrentDomain.SetupInformation);

            domain1.DoCallBack(AppDomainCallback);

            var rawValue = domain1.GetData(TestDataKey);

            Assert.IsType<bool>(rawValue);
            Assert.True((bool)rawValue);

            var domain2 = AppDomain.CreateDomain("Test", null, AppDomain.CurrentDomain.SetupInformation);

            domain2.DoCallBack(AppDomainCallback);

            rawValue = domain2.GetData(TestDataKey);

            Assert.IsType<bool>(rawValue);
            Assert.False((bool)rawValue);
        }
#endif
    }
}

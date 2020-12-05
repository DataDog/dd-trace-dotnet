#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class OwinTests : AspNetCoreMvcTestBase
    {
        public OwinTests(ITestOutputHelper output)
            : base("Owin.WebApi", output, topLevelOperationName: "owin.request", serviceVersion: "1.0.0")
        {
            Expectations.Clear();

            CreateTopLevelExpectation(topLevelOperationName: TopLevelOperationName, url: "/", httpMethod: "GET", httpStatus: "200", resourceUrl: "/", serviceVersion: ServiceVersion);
            CreateTopLevelExpectation(topLevelOperationName: TopLevelOperationName, url: "/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "/delay/?", serviceVersion: ServiceVersion);
            CreateTopLevelExpectation(topLevelOperationName: TopLevelOperationName, url: "/api/delay/0", httpMethod: "GET", httpStatus: "200", resourceUrl: "/api/delay/?", serviceVersion: ServiceVersion);
            CreateTopLevelExpectation(topLevelOperationName: TopLevelOperationName, url: "/not-found", httpMethod: "GET", httpStatus: "404", resourceUrl: "/not-found", serviceVersion: ServiceVersion);
            CreateTopLevelExpectation(topLevelOperationName: TopLevelOperationName, url: "/status-code/203", httpMethod: "GET", httpStatus: "203", resourceUrl: "/status-code/?", serviceVersion: ServiceVersion);

            CreateTopLevelExpectation(
                topLevelOperationName: TopLevelOperationName,
                url: "/status-code/500",
                httpMethod: "GET",
                httpStatus: "500",
                resourceUrl: "/status-code/?",
                serviceVersion: ServiceVersion,
                additionalCheck: span =>
                {
                    var failures = new List<string>();

                    if (span.Error == 0)
                    {
                        failures.Add($"Expected Error flag set within {span.Resource}");
                    }

                    if (SpanExpectation.GetTag(span, Tags.ErrorType) != null)
                    {
                        failures.Add($"Did not expect exception type within {span.Resource}");
                    }

                    var errorMessage = SpanExpectation.GetTag(span, Tags.ErrorMsg);

                    if (errorMessage != "The HTTP response has status code 500.")
                    {
                        failures.Add($"Expected specific error message within {span.Resource}. Found \"{errorMessage}\"");
                    }

                    return failures;
                });

            CreateTopLevelExpectation(
                topLevelOperationName: TopLevelOperationName,
                url: "/bad-request",
                httpMethod: "GET",
                httpStatus: "500",
                resourceUrl: "/bad-request",
                serviceVersion: ServiceVersion,
                additionalCheck: span =>
                {
                    var failures = new List<string>();

                    if (span.Error == 0)
                    {
                        failures.Add($"Expected Error flag set within {span.Resource}");
                    }

                    var errorMessage = SpanExpectation.GetTag(span, Tags.ErrorMsg);

                    if (errorMessage != "The HTTP response has status code 500.")
                    {
                        failures.Add($"Expected specific error message within {span.Resource}. Found \"{errorMessage}\"");
                    }

                    return failures;
                });
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            // No package versions are relevant because this is built-in
            await RunTraceTestOnSelfHosted(string.Empty);
        }
    }
}
#endif

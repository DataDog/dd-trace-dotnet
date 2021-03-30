using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetWebApi2TestData
    {
        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> WithoutFeatureFlag => new()
        {
            { "/api/environment", "GET api/environment", 200, false, null, null, EnvironmentTags },
            { "/api/absolute-route", "GET api/absolute-route", 200, false, null, null, AbsoluteRouteTags },
            { "/api/delay/0", "GET api/delay/{seconds}", 200, false, null, null, DelayTags },
            { "/api/delay-optional", "GET api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/api/delay-optional/1", "GET api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/api/delay-async/0", "GET api/delay-async/{seconds}", 200, false, null, null, DelayAsyncTags },
            { "/api/transient-failure/true", "GET api/transient-failure/{value}", 200, false, null, null, TransientFailureTags },
            { "/api/transient-failure/false", "GET api/transient-failure/{value}", 500, true, "System.ArgumentException", "Passed in value was not 'true': false", TransientFailureTags },
            { "/api/statuscode/201", "GET api/statuscode/{value}", 201, false, null, null, StatusCodeTags },
            { "/api/statuscode/503", "GET api/statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags },
            { "/api2/delay/0", "GET api2/delay/{value}", 200, false, null, null, ConventionDelayTags },
            { "/api2/optional", "GET api2/optional/{value}", 200, false, null, null, ConventionDelayOptionalTags },
            { "/api2/optional/1", "GET api2/optional/{value}", 200, false, null, null, ConventionDelayOptionalTags },
            { "/api2/delayAsync/0", "GET api2/delayasync/{value}", 200, false, null, null, ConventionDelayAsyncTags },
            { "/api2/transientfailure/true", "GET api2/transientfailure/{value}", 200, false, null, null, ConventionTransientFailureTags },
            { "/api2/transientfailure/false", "GET api2/transientfailure/{value}", 500, true, "System.ArgumentException", "Passed in value was not 'true': false", ConventionTransientFailureTags },
            { "/api2/statuscode/201", "GET api2/statuscode/{value}", 201, false, null, null, ConventionStatusCodeTags },
            { "/api2/statuscode/503", "GET api2/statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", ConventionStatusCodeTags },
        };

        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/api/environment", "GET /api/environment", 200, false, null, null, EnvironmentTags },
            { "/api/absolute-route", "GET /api/absolute-route", 200, false, null, null, AbsoluteRouteTags },
            { "/api/delay/0", "GET /api/delay/{seconds}", 200, false, null, null, DelayTags },
            { "/api/delay-optional", "GET /api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/api/delay-optional/1", "GET /api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/api/delay-async/0", "GET /api/delay-async/{seconds}", 200, false, null, null, DelayAsyncTags },
            { "/api/transient-failure/true", "GET /api/transient-failure/{value}", 200, false, null, null, TransientFailureTags },
            { "/api/transient-failure/false", "GET /api/transient-failure/{value}", 500, true, "System.ArgumentException", "Passed in value was not 'true': false", TransientFailureTags },
            { "/api/statuscode/201", "GET /api/statuscode/{value}", 201, false, null, null, StatusCodeTags },
            { "/api/statuscode/503", "GET /api/statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags },
            { "/api2/delay/0", "GET /api2/delay/{value}", 200, false, null, null, ConventionDelayTags },
            { "/api2/optional", "GET /api2/optional/{value}", 200, false, null, null, ConventionDelayOptionalTags },
            { "/api2/optional/1", "GET /api2/optional/{value}", 200, false, null, null, ConventionDelayOptionalTags },
            { "/api2/delayAsync/0", "GET /api2/delayasync/{value}", 200, false, null, null, ConventionDelayAsyncTags },
            { "/api2/transientfailure/true", "GET /api2/transientfailure/{value}", 200, false, null, null, ConventionTransientFailureTags },
            { "/api2/transientfailure/false", "GET /api2/transientfailure/{value}", 500, true, "System.ArgumentException", "Passed in value was not 'true': false", ConventionTransientFailureTags },
            { "/api2/statuscode/201", "GET /api2/statuscode/{value}", 201, false, null, null, ConventionStatusCodeTags },
            { "/api2/statuscode/503", "GET /api2/statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", ConventionStatusCodeTags },
        };

        private static SerializableDictionary EnvironmentTags => new()
        {
            { Tags.AspNetRoute, "api/environment" },
        };

        private static SerializableDictionary AbsoluteRouteTags => new()
        {
            { Tags.AspNetRoute, "api/absolute-route" },
        };

        private static SerializableDictionary TransientFailureTags => new()
        {
            { Tags.AspNetRoute, "api/transient-failure/{value}" },
        };

        private static SerializableDictionary DelayTags => new()
        {
            { Tags.AspNetRoute, "api/delay/{seconds}" },
        };

        private static SerializableDictionary DelayOptionalTags => new()
        {
            { Tags.AspNetRoute, "api/delay-optional/{seconds}" },
        };

        private static SerializableDictionary DelayAsyncTags => new()
        {
            { Tags.AspNetRoute, "api/delay-async/{seconds}" },
        };

        private static SerializableDictionary StatusCodeTags => new()
        {
            { Tags.AspNetRoute, "api/statuscode/{value}" },
        };

        private static string DefaultApiRoute => "api2/{action}/{value}";

        private static SerializableDictionary ConventionDelayTags => new()
        {
            { Tags.AspNetRoute, DefaultApiRoute },
            { Tags.AspNetController, "conventions" },
            { Tags.AspNetAction, "delay" },
        };

        private static SerializableDictionary ConventionDelayOptionalTags => new()
        {
            { Tags.AspNetRoute, DefaultApiRoute },
            { Tags.AspNetController, "conventions" },
            { Tags.AspNetAction, "optional" },
        };

        private static SerializableDictionary ConventionDelayAsyncTags => new()
        {
            { Tags.AspNetRoute, DefaultApiRoute },
            { Tags.AspNetController, "conventions" },
            { Tags.AspNetAction, "delayasync" },
        };

        private static SerializableDictionary ConventionTransientFailureTags => new()
        {
            { Tags.AspNetRoute, DefaultApiRoute },
            { Tags.AspNetController, "conventions" },
            { Tags.AspNetAction, "transientfailure" },
        };

        private static SerializableDictionary ConventionStatusCodeTags => new()
        {
            { Tags.AspNetRoute, DefaultApiRoute },
            { Tags.AspNetController, "conventions" },
            { Tags.AspNetAction, "statuscode" },
        };
    }
}

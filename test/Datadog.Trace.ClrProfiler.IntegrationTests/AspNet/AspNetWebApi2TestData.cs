using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetWebApi2TestData
    {
        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> Data => new()
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

        private static SerializableDictionary DelayHomeTags => new()
        {
            { Tags.AspNetRoute, "delay/{seconds}" },
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
    }
}
